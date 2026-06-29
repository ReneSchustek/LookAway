namespace LookAway.Core.Enums;

/// <summary>
/// Grund, aus dem eine laufende Pause beendet wurde.
/// </summary>
public enum BreakEndReason
{
    /// <summary>Die Pausendauer ist regulaer abgelaufen.</summary>
    Elapsed,

    /// <summary>Der Benutzer hat die Pause vorzeitig beendet (z. B. mit ESC).</summary>
    EndedByUser,
}
