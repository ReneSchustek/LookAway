namespace LookAway.Core.Domain;

/// <summary>
/// Stabile Lokalisierungs-Schlüssel für die Übungs-/Pausen-Hinweise pro
/// Pausenmodell. Die konkreten Texte liefert die Lokalisierung;
/// hier stehen bewusst nur die sprachneutralen Schlüssel, kein UI-Text.
/// </summary>
public static class BreakHintKeys
{
    /// <summary>Hinweis für <c>ShortBreaks</c> (Aufstehen, strecken, lüften).</summary>
    public const string ShortBreaks = "BreakHint.ShortBreaks";

    /// <summary>Gemeinsamer Hinweis für <c>ClassicPomodoro</c> und <c>ModifiedPomodoro</c>.</summary>
    public const string Pomodoro = "BreakHint.Pomodoro";

    /// <summary>Hinweis für <c>Ultradian</c> (mentaler Reset).</summary>
    public const string Ultradian = "BreakHint.Ultradian";

    /// <summary>Hinweis für <c>PhysicalCounter</c> (Chest Opener, Handgelenke).</summary>
    public const string PhysicalCounter = "BreakHint.PhysicalCounter";

    /// <summary>Hinweis für <c>TaskBased</c> (Meilenstein erreicht).</summary>
    public const string TaskBased = "BreakHint.TaskBased";

    /// <summary>Hinweis für <c>LegalCompliance</c> (gesetzliche Bildschirmpause).</summary>
    public const string LegalCompliance = "BreakHint.LegalCompliance";
}
