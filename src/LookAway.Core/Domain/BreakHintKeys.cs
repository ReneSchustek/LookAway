namespace LookAway.Core.Domain;

/// <summary>
/// Stabile Lokalisierungs-Schluessel fuer die Uebungs-/Pausen-Hinweise pro
/// Pausenmodell. Die konkreten Texte liefert die Lokalisierung;
/// hier stehen bewusst nur die sprachneutralen Schluessel, kein UI-Text.
/// </summary>
public static class BreakHintKeys
{
    /// <summary>Hinweis fuer <c>ShortBreaks</c> (Aufstehen, strecken, lueften).</summary>
    public const string ShortBreaks = "BreakHint.ShortBreaks";

    /// <summary>Gemeinsamer Hinweis fuer <c>ClassicPomodoro</c> und <c>ModifiedPomodoro</c>.</summary>
    public const string Pomodoro = "BreakHint.Pomodoro";

    /// <summary>Hinweis fuer <c>Ultradian</c> (mentaler Reset).</summary>
    public const string Ultradian = "BreakHint.Ultradian";

    /// <summary>Hinweis fuer <c>PhysicalCounter</c> (Chest Opener, Handgelenke).</summary>
    public const string PhysicalCounter = "BreakHint.PhysicalCounter";

    /// <summary>Hinweis fuer <c>TaskBased</c> (Meilenstein erreicht).</summary>
    public const string TaskBased = "BreakHint.TaskBased";

    /// <summary>Hinweis fuer <c>LegalCompliance</c> (gesetzliche Bildschirmpause).</summary>
    public const string LegalCompliance = "BreakHint.LegalCompliance";
}
