using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Domain;

/// <summary>
/// Basisklasse fuer alle vom Timer-Service ausgegebenen Domain-Events.
/// </summary>
public abstract record TimerEvent
{
    /// <summary>Zeitstempel des Events (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>Wird gefeuert, wenn die Arbeitsphase abgelaufen ist und eine Pause faellig ist.</summary>
/// <param name="Interval">Aktives Intervall.</param>
public sealed record BreakDueEvent(BreakInterval Interval) : TimerEvent;

/// <summary>Wird gefeuert, wenn die Pausenphase regulaer abgelaufen ist.</summary>
public sealed record BreakCompletedEvent : TimerEvent;

/// <summary>Wird gefeuert, wenn eine neue Arbeitsphase startet (nach Pause-Ende oder Resume).</summary>
public sealed record WorkResumedEvent : TimerEvent;

/// <summary>Wird gefeuert, wenn der Timer pausiert wurde.</summary>
/// <param name="PausedFromState">Der Zustand vor der Pause.</param>
/// <param name="PausedBySystem">True bei System-Suspend, False bei Benutzer-Pause.</param>
public sealed record TimerPausedEvent(TimerState PausedFromState, bool PausedBySystem) : TimerEvent;

/// <summary>Wird gefeuert, wenn der Timer gestoppt wurde (manuell oder Dispose).</summary>
public sealed record TimerStoppedEvent : TimerEvent;
