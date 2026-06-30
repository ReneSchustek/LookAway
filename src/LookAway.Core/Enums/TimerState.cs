namespace LookAway.Core.Enums;

/// <summary>
/// Zustände des Timer-Service.
/// </summary>
public enum TimerState
{
    /// <summary>Timer ist nicht aktiv.</summary>
    Idle,

    /// <summary>Arbeitsphase läuft.</summary>
    Working,

    /// <summary>Pause läuft (von der Engine ausgelöst).</summary>
    OnBreak,

    /// <summary>Timer ist pausiert (durch Benutzer oder System-Suspend).</summary>
    Paused,
}
