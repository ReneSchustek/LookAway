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
/// damit die Logik einheitlich getestet werden kann.
/// </remarks>
public sealed class TimerService : ITimerService, IDisposable
{
    /// <summary>Default-Tickintervall für den Hintergrund-Loop.</summary>
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
    private DateTimeOffset _pauseStartUtc;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    // Wird vom Hintergrund-Loop und von Power-Events außerhalb des Locks gelesen.
    private volatile bool _disposed;

    /// <summary>Erzeugt einen <see cref="TimerService"/> mit Default-Tickintervall.</summary>
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
    public void RestoreWorking(BreakInterval interval, TimeSpan workRemaining)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ThrowIfDisposed();

        lock (_lock)
        {
            // Restzeit auf einen gültigen Bereich begrenzen: nie negativ und nie
            // länger als eine volle Arbeitsphase (Schutz vor manipulierten/alten Werten).
            TimeSpan remaining = workRemaining < TimeSpan.Zero
                ? TimeSpan.Zero
                : (workRemaining > interval.WorkDuration ? interval.WorkDuration : workRemaining);

            _interval = interval;
            _phaseDuration = remaining;
            _phaseStartUtc = _clock.UtcNow;
            _state = TimerState.Working;
            _pausedBySystem = false;
            _pausedRemaining = TimeSpan.Zero;

            TimerServiceLog.TimerRestored(_logger, remaining, interval.BreakDuration);
            EnsureLoopStarted();
        }
    }

    /// <inheritdoc />
    public void StopCycle()
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
    public void ResumeAfterPause()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            // Nur eine echte Benutzer-Pause faktisch fortsetzen. Wenn der
            // Service nur durch System-Sleep pausiert ist, behält er die
            // Wiederaufnahme dem Power-Resume-Pfad vor.
            if (_state == TimerState.Paused && !_pausedBySystem)
            {
                ResumeInternal();
            }
        }
    }

    /// <inheritdoc />
    public void ResumeAfterAway(TimeSpan awayDuration)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            // Betrifft nur automatische Pausen (Idle). System-Pausen werden über
            // den Power-Resume-Pfad behandelt; Benutzer-Pausen über ResumeAfterPause().
            if (_state == TimerState.Paused && !_pausedBySystem)
            {
                ResumeOrRestart(awayDuration);
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
        _pauseStartUtc = _clock.UtcNow;
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
        // beim Resume aus einer Pause (OnBreak) verlässt sich der Konsument auf den State.
        if (resumedState == TimerState.Working)
        {
            EnqueueEvent(new WorkResumedEvent { Timestamp = _clock.UtcNow });
        }
    }

    /// <summary>
    /// Entscheidet beim Fortsetzen, ob die Restzeit weiterläuft oder eine frische
    /// Arbeitsphase beginnt: War die Abwesenheit mindestens so lang wie eine Pause,
    /// haben die Augen bereits geruht — dann wird neu gestartet.
    /// </summary>
    private void ResumeOrRestart(TimeSpan awayDuration)
    {
        if (_interval is not null && awayDuration >= _interval.BreakDuration)
        {
            TimerServiceLog.TimerRestartedAfterAway(_logger, _previousStateBeforePause, awayDuration);
            RestartWorkingPhase();
        }
        else
        {
            ResumeInternal();
            // Falls die Restzeit während der Abwesenheit abgelaufen wäre,
            // sofort den nächsten Phasenübergang ausführen.
            EvaluatePhase();
        }
    }

    /// <summary>
    /// Beginnt eine frische Arbeitsphase mit voller Arbeitsdauer. Lag die
    /// Abwesenheit in einer Pause, wird zuvor das reguläre Pausenende signalisiert
    /// (damit Overlay/Helligkeit/Medien sauber zurückgesetzt werden).
    /// </summary>
    private void RestartWorkingPhase()
    {
        _pausedBySystem = false;
        _pausedRemaining = TimeSpan.Zero;

        if (_previousStateBeforePause == TimerState.OnBreak)
        {
            TransitionToWorking();
            return;
        }

        BreakInterval interval = _interval!;
        _state = TimerState.Working;
        _phaseStartUtc = _clock.UtcNow;
        _phaseDuration = interval.WorkDuration;
        EnqueueEvent(new WorkResumedEvent { Timestamp = _clock.UtcNow });
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
                // Sleep zählt als Abwesenheit: War der Rechner mindestens eine
                // Pausenlänge im Standby, beginnt eine frische Arbeitsphase.
                ResumeOrRestart(_clock.UtcNow - _pauseStartUtc);
            }
        }
    }

    private void EnsureLoopStarted()
    {
        // Nur weiterlaufen lassen, wenn der Loop aktiv UND nicht bereits abgebrochen
        // ist (sonst würde ein Start nach Stop keinen neuen Loop aufsetzen).
        if (_loopTask is { IsCompleted: false } && _loopCts is { IsCancellationRequested: false })
        {
            return;
        }

        _loopCts?.Dispose();
        _loopCts = new CancellationTokenSource();
        // Token lokal festhalten: ein zwischenzeitliches StopCycle()+Start() darf den
        // gerade gestarteten Loop nicht mit dem Token einer neuen CTS laufen lassen.
        CancellationToken token = _loopCts.Token;
        _loopTask = Task.Run(() => RunLoopAsync(token));
    }

    private void StopLoop()
    {
        // Zugriff auf das Lebenszyklus-Feld konsistent zu EnsureLoopStarted unter
        // demselben Lock. Die Entsorgung erfolgt in EnsureLoopStarted (vor dem
        // Neustart) bzw. in Dispose — hier wird nur abgebrochen.
        lock (_lock)
        {
            if (_loopCts is null)
            {
                return;
            }

            try
            {
                _loopCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Bereits freigegeben (paralleler Stop/Dispose) — kein Handlungsbedarf.
            }
        }
    }

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
        // Letzte Verteidigungslinie: der Tick-Loop trägt den gesamten Pausen-Zyklus.
        // Ein unerwarteter Fehler wird strukturiert protokolliert, damit der Loop nicht
        // stillschweigend endet; der Filter hält den Fehler diagnostizierbar.
        catch (Exception ex) when (LogLoopCrash(ex))
        {
            // Der Filter hat bereits protokolliert; der Loop terminiert kontrolliert.
        }
    }

    private bool LogLoopCrash(Exception exception)
    {
        TimerServiceLog.LoopCrashed(_logger, exception);
        return true;
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
