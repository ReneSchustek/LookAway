namespace LookAway.Application.Localization;

/// <summary>
/// Sprachneutrale Schluessel des First-Run-Wizards. Texte liegen in
/// den eingebetteten Sprachtabellen (Data-Schicht).
/// </summary>
public static class WelcomeTextKeys
{
    /// <summary>Fenstertitel / Begruessung.</summary>
    public const string Title = "Welcome.Title";

    /// <summary>Einleitungstext.</summary>
    public const string Intro = "Welcome.Intro";

    /// <summary>Schritt-Ueberschrift "Sprache".</summary>
    public const string StepLanguage = "Welcome.Step.Language";

    /// <summary>Schritt-Ueberschrift "Pausenmodell".</summary>
    public const string StepModel = "Welcome.Step.Model";

    /// <summary>Schritt-Ueberschrift "Autostart".</summary>
    public const string StepAutoStart = "Welcome.Step.AutoStart";

    /// <summary>Hinweis zum Autostart.</summary>
    public const string AutoStartHint = "Welcome.AutoStartHint";

    /// <summary>Hinweis zum Schliessen ohne Abschluss.</summary>
    public const string CloseHint = "Welcome.CloseHint";

    /// <summary>Fortschrittsanzeige (Formatstring {0}/{1}).</summary>
    public const string StepProgress = "Welcome.StepProgress";

    /// <summary>Beschriftung des Zurueck-Buttons.</summary>
    public const string ButtonBack = "Welcome.Button.Back";

    /// <summary>Beschriftung des Weiter-Buttons.</summary>
    public const string ButtonNext = "Welcome.Button.Next";

    /// <summary>Beschriftung des Fertig-Buttons.</summary>
    public const string ButtonFinish = "Welcome.Button.Finish";
}
