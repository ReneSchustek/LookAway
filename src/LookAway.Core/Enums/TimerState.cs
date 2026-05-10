namespace LookAway.Core.Enums;

/// <summary>
/// Zustaende des Timer-Service.
/// </summary>
public enum TimerState
{
    /// <summary>Timer ist nicht aktiv.</summary>
    Idle,

    /// <summary>Arbeitsphase laeuft.</summary>
    Working,

    /// <summary>Pause laeuft (von der Engine ausgeloest).</summary>
    OnBreak,

    /// <summary>Timer ist pausiert (durch Benutzer oder System-Suspend).</summary>
    Paused,
}
