using System.Threading.Channels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// State-Machine fuer Pausen-Erinnerungen. Konsumiert <see cref="IClock"/>
/// fuer Zeitabfragen und <see cref="IPowerModeWatcher"/> fuer Sleep-/Resume-
/// Events. Produziert einen Strom von <see cref="TimerEvent"/>-Instanzen
/// ueber einen unbeschraenkten Channel.
/// </summary>
/// <remarks>
/// Zustandswechsel werden von einem Hintergrund-Loop und von externen
/// Power-Events ausgeloest. Beide Pfade gehen durch <see cref="Tick"/>,
/// damit die Logik einheitlich getestet werden kann.
/// </remarks>
public sealed class TimerService : ITimerService, IDisposable
{
    /// <summary>Default-Tickintervall fuer den Hintergrund-Loop.</summary>
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromMilliseconds(250);

    private readonly IClock _clock;
    private readonly IPowerModeWatcher _powerWatcher;
    private readonly ILogger<TimerService> _logger;
    private readonly TimeSpan _tickInterval;
    private readonly Channel<TimerEvent> _events;
    private readonly Lock _lock = new();

    private TimerState _state = TimerState.Idle;
    private BreakInterval? _interval;
    private DateTimeOffset _phaseStartUtc;
    private TimeSpan _phaseDuration;
    private TimeSpan _pausedRemaining;
    private TimerState _previousStateBeforePause;
    private bool _pausedBySystem;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _disposed;

    /// <summary>Erzeugt einen <see cref="TimerService"/> mit Default-Tickintervall.</summary>
    public TimerService(IClock clock, IPowerModeWatcher powerWatcher, ILogger<TimerService> logger)
        : this(clock, powerWatcher, logger, DefaultTickInterval)
    {
    }

    /// <summary>
    /// Konstruktor mit explizitem Tickintervall — vorgesehen fuer Tests,
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
        _logger = logger;
        _tickInterval = tickInterval;
        _events = Channel.CreateUnbounded<TimerEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

        _powerWatcher.Suspending += OnSystemSuspending;
        _powerWatcher.Resuming += OnSystemResuming;
    }

    /// <inheritdoc />
    public TimerState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public BreakInterval? CurrentInterval
    {
        get
        {
            lock (_lock)
            {
                return _interval;
            }
        }
    }

    /// <inheritdoc />
    public TimeSpan Remaining
    {
        get
        {
            lock (_lock)
            {
                return ComputeRemaining();
            }
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TimerEvent> Events => _events.Reader.ReadAllAsync();

    /// <inheritdoc />
    public void Start(BreakInterval interval)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ThrowIfDisposed();

        lock (_lock)
        {
            _interval = interval;
            _phaseDuration = interval.WorkDuration;
            _phaseStartUtc = _clock.UtcNow;
            _state = TimerState.Working;
            _pausedBySystem = false;
            _pausedRemaining = TimeSpan.Zero;

            TimerServiceLog.TimerStarted(_logger, interval.WorkDuration, interval.BreakDuration);
            EnsureLoopStarted();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        ThrowIfDisposed();

        bool wasActive;
        lock (_lock)
        {
            wasActive = _state != TimerState.Idle;
            _state = TimerState.Idle;
            _interval = null;
            _phaseDuration = TimeSpan.Zero;
            _pausedRemaining = TimeSpan.Zero;
            _pausedBySystem = false;

            if (wasActive)
            {
                TimerServiceLog.TimerStopped(_logger);
                EnqueueEvent(new TimerStoppedEvent());
            }
        }

        StopLoop();
    }

    /// <inheritdoc />
    public void Pause()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            PauseInternal(bySystem: false);
        }
    }

    /// <inheritdoc />
    public void Resume()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            // Nur eine echte Benutzer-Pause faktisch fortsetzen. Wenn der
            // Service nur durch System-Sleep pausiert ist, behaelt er die
            // Wiederaufnahme dem Power-Resume-Pfad vor.
            if (_state == TimerState.Paused && !_pausedBySystem)
            {
                ResumeInternal();
            }
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
            EvaluatePhase();
        }
    }

    /// <summary>
    /// Beendet den Hintergrund-Loop, gibt den Channel frei und meldet
    /// die Power-Hooks ab.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _powerWatcher.Suspending -= OnSystemSuspending;
        _powerWatcher.Resuming -= OnSystemResuming;

        StopLoop();
        _loopCts?.Dispose();
        _ = _events.Writer.TryComplete();
    }

    private void EvaluatePhase()
    {
        if (_state == TimerState.Working)
        {
            if (PhaseElapsed())
            {
                TransitionToOnBreak();
            }
        }
        else if (_state == TimerState.OnBreak)
        {
            if (PhaseElapsed())
            {
                TransitionToWorking();
            }
        }
    }

    private bool PhaseElapsed() => _clock.UtcNow >= _phaseStartUtc + _phaseDuration;

    private void TransitionToOnBreak()
    {
        BreakInterval interval = _interval!;
        _state = TimerState.OnBreak;
        _phaseStartUtc = _clock.UtcNow;
        _phaseDuration = interval.BreakDuration;

        TimerServiceLog.BreakDue(_logger, interval.WorkDuration);
        EnqueueEvent(new BreakDueEvent(interval) { Timestamp = _clock.UtcNow });
    }

    private void TransitionToWorking()
    {
        BreakInterval interval = _interval!;
        _state = TimerState.Working;
        _phaseStartUtc = _clock.UtcNow;
        _phaseDuration = interval.WorkDuration;

        TimerServiceLog.BreakCompleted(_logger, interval.BreakDuration);
        EnqueueEvent(new BreakCompletedEvent { Timestamp = _clock.UtcNow });
        EnqueueEvent(new WorkResumedEvent { Timestamp = _clock.UtcNow });
    }

    private void PauseInternal(bool bySystem)
    {
        if (_state != TimerState.Working && _state != TimerState.OnBreak)
        {
            return;
        }

        _pausedRemaining = ComputeRemaining();
        _previousStateBeforePause = _state;
        _pausedBySystem = bySystem;
        _state = TimerState.Paused;

        TimerServiceLog.TimerPaused(_logger, _previousStateBeforePause, bySystem, _pausedRemaining);
        EnqueueEvent(new TimerPausedEvent(_previousStateBeforePause, bySystem) { Timestamp = _clock.UtcNow });
    }

    private void ResumeInternal()
    {
        TimerState resumedState = _previousStateBeforePause;
        _state = resumedState;
        _phaseStartUtc = _clock.UtcNow;
        _phaseDuration = _pausedRemaining;
        _pausedBySystem = false;
        _pausedRemaining = TimeSpan.Zero;

        TimerServiceLog.TimerResumed(_logger, resumedState);

        // Nach Resume in eine Arbeitsphase wird ein WorkResumed-Event emittiert;
        // beim Resume aus einer Pause (OnBreak) verlaesst sich der Konsument auf den State.
        if (resumedState == TimerState.Working)
        {
            EnqueueEvent(new WorkResumedEvent { Timestamp = _clock.UtcNow });
        }
    }

    private TimeSpan ComputeRemaining()
    {
        if (_state == TimerState.Idle)
        {
            return TimeSpan.Zero;
        }

        if (_state == TimerState.Paused)
        {
            return _pausedRemaining;
        }

        TimeSpan remaining = (_phaseStartUtc + _phaseDuration) - _clock.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private void OnSystemSuspending(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_state is TimerState.Working or TimerState.OnBreak)
            {
                PauseInternal(bySystem: true);
            }
        }
    }

    private void OnSystemResuming(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_state == TimerState.Paused && _pausedBySystem)
            {
                ResumeInternal();
                // Falls die Restzeit waehrend des Sleep abgelaufen waere,
                // sofort den naechsten Phasenuebergang ausfuehren.
                EvaluatePhase();
            }
        }
    }

    private void EnsureLoopStarted()
    {
        if (_loopTask is not null && !_loopTask.IsCompleted)
        {
            return;
        }

        _loopCts?.Dispose();
        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token));
    }

    private void StopLoop()
    {
        CancellationTokenSource? cts = _loopCts;
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _loopCts = null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Der Loop ist die letzte Verteidigungslinie. Eine unbehandelte Exception darf den Service nicht stillschweigend beenden — sie wird strukturiert geloggt und der Loop terminiert kontrolliert.")]
    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_tickInterval, cancellationToken).ConfigureAwait(false);
                Tick();
            }
        }
        catch (OperationCanceledException)
        {
            // erwarteter Beendigungspfad
        }
        catch (Exception ex)
        {
            TimerServiceLog.LoopCrashed(_logger, ex);
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
