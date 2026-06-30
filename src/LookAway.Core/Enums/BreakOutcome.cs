namespace LookAway.Core.Enums;

/// <summary>
/// Ergebnis einer angebotenen Pause-Erinnerung.
/// </summary>
public enum BreakOutcome
{
    /// <summary>Pause wurde gemacht.</summary>
    Taken,

    /// <summary>Pause wurde verschoben (Snooze).</summary>
    Snoozed,

    /// <summary>Pause wurde übersprungen.</summary>
    Skipped,
}
