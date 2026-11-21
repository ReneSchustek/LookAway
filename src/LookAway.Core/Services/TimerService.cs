using System.Threading.Channels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Core.Services;

/// <summary>
/// State-Machine für Pausen-Erinnerungen. Konsumiert <see cref="IClock"/>
/// für Zeitabfragen und <see cref="IPowerModeWatcher"/> für Sleep-/Resume-
/// Events. Produziert einen Strom von <see cref="TimerEvent"/>-Instanzen
/// über einen unbeschränkten Channel.
/// </summary>
/// <remarks>
/// Zustandswechsel werden von einem Hintergrund-Loop und von externen
/// Power-Events ausgelöst. Beide Pfade gehen durch <see cref="Tick"/>,
/// damit die Logik einheitlich getestet werden kann. Die Übergänge selbst stehen
/// in <see cref="TimerStateMachine"/>; dieser Dienst macht sie threadsicher und
/// betreibt Takt, Energie-Ereignisse und den Ereignisstrom.
/// </remarks>
public sealed class TimerService : ITimerService, IDisposable
{
    /// <summary>
    /// Default-Tickintervall für den Hintergrund-Loop. Ein Phasenwechsel kommt so
    /// höchstens eine Viertelsekunde nach seiner Fälligkeit; ein Tick vergleicht nur
    /// zwei Zeitpunkte und kostet entsprechend wenig.
    /// </summary>
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromMilliseconds(250);

    private readonly IClock _clock;
    private readonly IPowerModeWatcher _powerWatcher;
    private readonly Channel<TimerEvent> _events;
    private readonly Lock _lock = new();
    private readonly TimerStateMachine _machine;
    private readonly TimerTickLoop _loop;
    // Wird vom Hintergrund-Loop und von Power-Events außerhalb des Locks gelesen.
    private volatile bool _disposed;

    /// <summary>Erzeugt einen <see cref="TimerService"/> mit Default-Tickintervall.</summary>
    /// <param name="clock">Uhr für Phasen und Restzeit.</param>
    /// <param name="powerWatcher">Meldet Standby und Aufwachen.</param>
    /// <param name="logger">Protokolliert Übergänge und einen abgestürzten Takt.</param>
    public TimerService(IClock clock, IPowerModeWatcher powerWatcher, ILogger<TimerService> logger)
        : this(clock, powerWatcher, logger, DefaultTickInterval)
    {
    }

    /// <summary>
    /// Konstruktor mit explizitem Tickintervall — vorgesehen für Tests,
    /// die den Loop-Takt selbst kontrollieren wollen.
    /// </summary>
    internal TimerService(IClock clock, IPowerModeWatcher powerWatcher, ILogger<TimerService> logger, TimeSpan tickInterval)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(powerWatcher);
        ArgumentNullException.ThrowIfNull(logger);
        if (tickInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tickInterval), tickInterval, "tickInterval muss positiv sein.");
        }

        _clock = clock;
        _powerWatcher = powerWatcher;
        // Unbeschränkt, weil je Phase nur eine Handvoll Ereignisse entsteht. Ein
        // beschränkter Kanal müsste entscheiden, welches er verwirft, und jedes trägt
        // einen Zustandswechsel, den der Pausenablauf braucht.
        _events = Channel.CreateUnbounded<TimerEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });
        _machine = new TimerStateMachine(clock, logger, EnqueueEvent);
        _loop = new TimerTickLoop(tickInterval, Tick, exception => TimerServiceLog.LoopCrashed(logger, exception));

        _powerWatcher.Suspending += OnSystemSuspending;
        _powerWatcher.Resuming += OnSystemResuming;
    }

    /// <inheritdoc />
    public TimerState State => Locked(() => _machine.State);

    /// <inheritdoc />
    public BreakInterval? CurrentInterval => Locked(() => _machine.Interval);

    /// <inheritdoc />
    public TimeSpan Remaining => Locked(() => _machine.Remaining);

    /// <inheritdoc />
    public IAsyncEnumerable<TimerEvent> Events => _events.Reader.ReadAllAsync();

    /// <inheritdoc />
    public void Start(BreakInterval interval)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ThrowIfDisposed();

        lock (_lock)
        {
            _machine.Start(interval);
            EnsureLoopStarted();
        }
    }

    /// <inheritdoc />
    public void RestoreWorking(BreakInterval interval, TimeSpan workRemaining)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ThrowIfDisposed();

        lock (_lock)
        {
            _machine.Restore(interval, workRemaining);
            EnsureLoopStarted();
        }
    }

    /// <inheritdoc />
    public void StopCycle()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _machine.Stop();
        }

        StopLoop();
    }

    /// <inheritdoc />
    public void Pause()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _machine.Pause(bySystem: false);
        }
    }

    /// <inheritdoc />
    public void ResumeAfterPause()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _machine.ResumeUserPause();
        }
    }

    /// <inheritdoc />
    public void ResumeAfterAway(TimeSpan awayDuration)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _machine.ResumeAfterAway(awayDuration);
        }
    }

    /// <summary>
    /// Wertet eine eventuelle Phasen-Beendigung aus und erzeugt die
    /// entsprechenden Events. Wird sowohl vom Hintergrund-Loop als auch
    /// nach Power-Resume aufgerufen.
    /// </summary>
    internal void Tick()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            _machine.Evaluate();
        }
    }

    /// <summary>
    /// Beendet den Hintergrund-Loop, gibt den Channel frei und meldet
    /// die Power-Hooks ab.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _powerWatcher.Suspending -= OnSystemSuspending;
            _powerWatcher.Resuming -= OnSystemResuming;

            // Abbrechen und Entsorgen der Loop-CTS unter demselben Lock wie
            // EnsureLoopStarted, damit kein paralleler Start eine bereits entsorgte
            // (oder frisch erzeugte) CTS trifft.
            _loop.Dispose();

            _ = _events.Writer.TryComplete();
        }
    }

    private T Locked<T>(Func<T> read)
    {
        lock (_lock)
        {
            return read();
        }
    }

    private void OnSystemSuspending(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _machine.SystemSuspending();
            }
        }
    }

    private void OnSystemResuming(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _machine.SystemResuming();
            }
        }
    }

    // Erwartet den gehaltenen Lock. Nach Dispose keinen neuen Loop mehr aufsetzen; Dispose
    // entsorgt die Abbruchquelle unter demselben Lock.
    private void EnsureLoopStarted()
    {
        if (!_disposed)
        {
            _loop.EnsureRunning();
        }
    }

    private void StopLoop()
    {
        // Derselbe Lock wie in EnsureLoopStarted. Abgebrochen wird nur; entsorgt wird die
        // Abbruchquelle vor dem Neustart oder in Dispose.
        lock (_lock)
        {
            _loop.Cancel();
        }
    }

    private void EnqueueEvent(TimerEvent evt)
    {
        TimerEvent stamped = evt.Timestamp == default
            ? evt with { Timestamp = _clock.UtcNow }
            : evt;
        _ = _events.Writer.TryWrite(stamped);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
