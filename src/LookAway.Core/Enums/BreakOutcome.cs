namespace LookAway.Core.Enums;

/// <summary>
/// Ergebnis einer angebotenen Pause-Erinnerung (BRIEF018).
/// </summary>
public enum BreakOutcome
{
    /// <summary>Pause wurde gemacht.</summary>
    Taken,

    /// <summary>Pause wurde verschoben (Snooze).</summary>
    Snoozed,

    /// <summary>Pause wurde uebersprungen.</summary>
    Skipped,
}
