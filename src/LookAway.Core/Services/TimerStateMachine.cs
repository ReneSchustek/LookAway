using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Core.Services;

/// <summary>
/// Der Zustandsautomat des Pausen-Timers: Arbeitsphase, Pausenphase, angehaltener Timer und die
/// Übergänge dazwischen, samt Restzeit und der Regel, nach einer langen Abwesenheit neu zu
/// beginnen. Jeder Übergang meldet sein Ereignis über den übergebenen Kanal.
/// </summary>
/// <remarks>
/// Nicht threadsicher: Der <see cref="TimerService"/> ruft ihn nur unter seiner Sperre auf und
/// kümmert sich um Hintergrund-Takt, Energie-Ereignisse und den Ereignisstrom.
/// </remarks>
internal sealed class TimerStateMachine
{
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly Action<TimerEvent> _emit;

    private BreakInterval? _interval;
    private DateTimeOffset _phaseStartUtc;
    private TimeSpan _phaseDuration;
    private TimeSpan _pausedRemaining;
    private TimerState _previousStateBeforePause;
    private bool _pausedBySystem;
    private DateTimeOffset _pauseStartUtc;

    /// <summary>Erzeugt den Automaten im Leerlauf.</summary>
    /// <param name="clock">Uhr für Phasenbeginn und Restzeit.</param>
    /// <param name="logger">Protokolliert jeden Übergang.</param>
    /// <param name="emit">Nimmt die Ereignisse der Übergänge entgegen.</param>
    public TimerStateMachine(IClock clock, ILogger logger, Action<TimerEvent> emit)
    {
        _clock = clock;
        _logger = logger;
        _emit = emit;
    }

    /// <summary>Aktueller Zustand.</summary>
    public TimerState State { get; private set; } = TimerState.Idle;

    /// <summary>Laufendes Intervall; <c>null</c> im Leerlauf.</summary>
    public BreakInterval? Interval => _interval;

    /// <summary>Restzeit der laufenden oder angehaltenen Phase; im Leerlauf null.</summary>
    public TimeSpan Remaining => State switch
    {
        TimerState.Idle => TimeSpan.Zero,
        TimerState.Paused => _pausedRemaining,
        _ => Max(TimeSpan.Zero, (_phaseStartUtc + _phaseDuration) - _clock.UtcNow),
    };

    /// <summary>Beginnt einen Zyklus mit voller Arbeitsphase.</summary>
    /// <param name="interval">Arbeits- und Pausendauer.</param>
    public void Start(BreakInterval interval)
    {
        BeginWorking(interval, interval.WorkDuration);
        TimerServiceLog.TimerStarted(_logger, interval.WorkDuration, interval.BreakDuration);
    }

    /// <summary>Setzt einen Zyklus mit gemerkter Restzeit fort.</summary>
    /// <param name="interval">Arbeits- und Pausendauer.</param>
    /// <param name="workRemaining">Gemerkte Restzeit der Arbeitsphase.</param>
    public void Restore(BreakInterval interval, TimeSpan workRemaining)
    {
        // Restzeit auf einen gültigen Bereich begrenzen: nie negativ und nie
        // länger als eine volle Arbeitsphase (Schutz vor manipulierten/alten Werten).
        TimeSpan remaining = workRemaining < TimeSpan.Zero
            ? TimeSpan.Zero
            : (workRemaining > interval.WorkDuration ? interval.WorkDuration : workRemaining);

        BeginWorking(interval, remaining);
        TimerServiceLog.TimerRestored(_logger, remaining, interval.BreakDuration);
    }

    /// <summary>Beendet den Zyklus; ein laufender meldet sein Ende.</summary>
    public void Stop()
    {
        bool wasActive = State != TimerState.Idle;
        State = TimerState.Idle;
        _interval = null;
        _phaseDuration = TimeSpan.Zero;
        _pausedRemaining = TimeSpan.Zero;
        _pausedBySystem = false;

        if (wasActive)
        {
            TimerServiceLog.TimerStopped(_logger);
            _emit(new TimerStoppedEvent());
        }
    }

    /// <summary>Wechselt die Phase, wenn ihre Zeit abgelaufen ist.</summary>
    public void Evaluate()
    {
        if (State == TimerState.Working && PhaseElapsed())
        {
            TransitionToOnBreak();
        }
        else if (State == TimerState.OnBreak && PhaseElapsed())
        {
            TransitionToWorking();
        }
    }

    /// <summary>Hält eine laufende Arbeits- oder Pausenphase an.</summary>
    /// <param name="bySystem">Angehalten durch Standby statt durch den Benutzer?</param>
    public void Pause(bool bySystem)
    {
        if (State != TimerState.Working && State != TimerState.OnBreak)
        {
            return;
        }

        _pausedRemaining = Remaining;
        _previousStateBeforePause = State;
        _pausedBySystem = bySystem;
        _pauseStartUtc = _clock.UtcNow;
        State = TimerState.Paused;

        TimerServiceLog.TimerPaused(_logger, _previousStateBeforePause, bySystem, _pausedRemaining);
        _emit(new TimerPausedEvent(_previousStateBeforePause, bySystem) { Timestamp = _clock.UtcNow });
    }

    /// <summary>
    /// Setzt eine Pause des Benutzers fort. Eine Pause durch Standby bleibt dem Fortsetzen nach dem
    /// Aufwachen vorbehalten.
    /// </summary>
    public void ResumeUserPause()
    {
        if (IsPausedByUser)
        {
            ResumeInternal();
        }
    }

    /// <summary>
    /// Setzt nach einer Abwesenheit (Inaktivität) fort. War sie so lang wie eine Pause, beginnt
    /// eine frische Arbeitsphase.
    /// </summary>
    /// <param name="awayDuration">Dauer der Abwesenheit.</param>
    public void ResumeAfterAway(TimeSpan awayDuration)
    {
        if (IsPausedByUser)
        {
            ResumeOrRestart(awayDuration);
        }
    }

    /// <summary>Der Rechner geht in den Standby: eine laufende Phase wird angehalten.</summary>
    public void SystemSuspending()
    {
        if (State is TimerState.Working or TimerState.OnBreak)
        {
            Pause(bySystem: true);
        }
    }

    /// <summary>
    /// Der Rechner wacht auf. Standby zählt als Abwesenheit: War er mindestens eine Pausenlänge
    /// im Standby, beginnt eine frische Arbeitsphase.
    /// </summary>
    public void SystemResuming()
    {
        if (State == TimerState.Paused && _pausedBySystem)
        {
            ResumeOrRestart(_clock.UtcNow - _pauseStartUtc);
        }
    }

    // Automatische und Benutzer-Pausen; System-Pausen laufen über den Power-Resume-Pfad.
    private bool IsPausedByUser => State == TimerState.Paused && !_pausedBySystem;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private bool PhaseElapsed() => _clock.UtcNow >= _phaseStartUtc + _phaseDuration;

    private void BeginWorking(BreakInterval interval, TimeSpan duration)
    {
        _interval = interval;
        _phaseDuration = duration;
        _phaseStartUtc = _clock.UtcNow;
        State = TimerState.Working;
        _pausedBySystem = false;
        _pausedRemaining = TimeSpan.Zero;
    }

    private void TransitionToOnBreak()
    {
        BreakInterval interval = _interval!; // Arbeits- und Pausenphase setzen ein Intervall voraus; Stop nimmt beides zusammen weg
        State = TimerState.OnBreak;
        _phaseStartUtc = _clock.UtcNow;
        _phaseDuration = interval.BreakDuration;

        TimerServiceLog.BreakDue(_logger, interval.WorkDuration);
        _emit(new BreakDueEvent(interval) { Timestamp = _clock.UtcNow });
    }

    private void TransitionToWorking()
    {
        BreakInterval interval = _interval!; // Arbeits- und Pausenphase setzen ein Intervall voraus; Stop nimmt beides zusammen weg
        State = TimerState.Working;
        _phaseStartUtc = _clock.UtcNow;
        _phaseDuration = interval.WorkDuration;

        TimerServiceLog.BreakCompleted(_logger, interval.BreakDuration);
        _emit(new BreakCompletedEvent { Timestamp = _clock.UtcNow });
        _emit(new WorkResumedEvent { Timestamp = _clock.UtcNow });
    }

    private void ResumeInternal()
    {
        TimerState resumedState = _previousStateBeforePause;
        State = resumedState;
        _phaseStartUtc = _clock.UtcNow;
        _phaseDuration = _pausedRemaining;
        _pausedBySystem = false;
        _pausedRemaining = TimeSpan.Zero;

        TimerServiceLog.TimerResumed(_logger, resumedState);

        // Nach Resume in eine Arbeitsphase wird ein WorkResumed-Event emittiert;
        // beim Resume aus einer Pause (OnBreak) verlässt sich der Konsument auf den State.
        if (resumedState == TimerState.Working)
        {
            _emit(new WorkResumedEvent { Timestamp = _clock.UtcNow });
        }
    }

    // Entscheidet beim Fortsetzen, ob die Restzeit weiterläuft oder eine frische Arbeitsphase
    // beginnt: War die Abwesenheit mindestens so lang wie eine Pause, haben die Augen bereits
    // geruht — dann wird neu gestartet.
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
            Evaluate();
        }
    }

    // Beginnt eine frische Arbeitsphase mit voller Arbeitsdauer. Lag die Abwesenheit in einer
    // Pause, wird zuvor das reguläre Pausenende signalisiert (damit Overlay/Helligkeit/Medien
    // sauber zurückgesetzt werden).
    private void RestartWorkingPhase()
    {
        _pausedBySystem = false;
        _pausedRemaining = TimeSpan.Zero;

        if (_previousStateBeforePause == TimerState.OnBreak)
        {
            TransitionToWorking();
            return;
        }

        BreakInterval interval = _interval!; // Arbeits- und Pausenphase setzen ein Intervall voraus; Stop nimmt beides zusammen weg
        BeginWorking(interval, interval.WorkDuration);
        _emit(new WorkResumedEvent { Timestamp = _clock.UtcNow });
    }
}
