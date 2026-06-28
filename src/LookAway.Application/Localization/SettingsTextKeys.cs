using LookAway.Core.Enums;

namespace LookAway.Application.Localization;

/// <summary>
/// Sprachneutrale Schluessel der Settings-Oberflaeche. Die zugehoerigen Texte
/// liegen in den eingebetteten Sprachtabellen (Data-Schicht, BRIEF010).
/// </summary>
public static class SettingsTextKeys
{
    /// <summary>Fenstertitel.</summary>
    public const string Title = "Settings.Title";

    /// <summary>Tab "Allgemein".</summary>
    public const string TabGeneral = "Settings.Tab.General";

    /// <summary>Tab "Pausenmodell".</summary>
    public const string TabModel = "Settings.Tab.Model";

    /// <summary>Tab "Eigene Intervalle".</summary>
    public const string TabIntervals = "Settings.Tab.Intervals";

    /// <summary>Tab "Ueber LookAway".</summary>
    public const string TabAbout = "Settings.Tab.About";

    /// <summary>Tab "Sound".</summary>
    public const string TabSound = "Settings.Tab.Sound";

    /// <summary>Beschriftung "Ton abspielen".</summary>
    public const string SoundEnableLabel = "Settings.Sound.EnableLabel";

    /// <summary>Beschriftung der Ton-Auswahl.</summary>
    public const string SoundSelectLabel = "Settings.Sound.SelectLabel";

    /// <summary>Beschriftung der Lautstaerke.</summary>
    public const string SoundVolumeLabel = "Settings.Sound.VolumeLabel";

    /// <summary>Beschriftung des Vorhoer-Buttons.</summary>
    public const string SoundPreviewButton = "Settings.Sound.PreviewButton";

    /// <summary>Beschriftung des Sprach-Dropdowns.</summary>
    public const string LanguageLabel = "Settings.Language.Label";

    /// <summary>Beschriftung der Autostart-Checkbox.</summary>
    public const string AutoStartLabel = "Settings.AutoStart.Label";

    /// <summary>Beschriftung der Auto-Pause-Checkbox.</summary>
    public const string IdlePauseLabel = "Settings.Idle.PauseLabel";

    /// <summary>Beschriftung der Inaktivitaetsschwelle.</summary>
    public const string IdleThresholdLabel = "Settings.Idle.ThresholdLabel";

    /// <summary>Beschriftung der DND-Checkbox.</summary>
    public const string FullscreenSuppressLabel = "Settings.Fullscreen.SuppressLabel";

    /// <summary>Beschriftung der Modellauswahl.</summary>
    public const string ModelLabel = "Settings.Model.Label";

    /// <summary>Beschriftung des "Eigene Dauern verwenden"-Schalters.</summary>
    public const string IntervalsUseCustom = "Settings.Intervals.UseCustom";

    /// <summary>Hinweistext im Intervall-Bereich.</summary>
    public const string IntervalsHint = "Settings.Intervals.Hint";

    /// <summary>Beschriftung der Arbeitsdauer.</summary>
    public const string WorkLabel = "Settings.Intervals.WorkLabel";

    /// <summary>Beschriftung der Pausendauer.</summary>
    public const string BreakLabel = "Settings.Intervals.BreakLabel";

    /// <summary>Hinweis auf den erlaubten Arbeitsdauer-Bereich (Formatstring {0}/{1}).</summary>
    public const string WorkRangeHint = "Settings.Intervals.WorkRangeHint";

    /// <summary>Validierungsmeldung Arbeitsdauer (Formatstring {0}/{1}).</summary>
    public const string ValidationWorkRange = "Settings.Validation.WorkRange";

    /// <summary>Validierungsmeldung Pausendauer (Formatstring {0}/{1}).</summary>
    public const string ValidationBreakRange = "Settings.Validation.BreakRange";

    /// <summary>Beschriftung "Version".</summary>
    public const string AboutVersionLabel = "Settings.About.VersionLabel";

    /// <summary>Beschriftung "Lizenz".</summary>
    public const string LicenseLabel = "Settings.About.LicenseLabel";

    /// <summary>Lizenztext.</summary>
    public const string License = "Settings.About.License";

    /// <summary>Beschriftung "Dokumentation".</summary>
    public const string DocsLabel = "Settings.About.DocsLabel";

    /// <summary>URL zur Dokumentation.</summary>
    public const string DocsUrl = "Settings.About.DocsUrl";

    /// <summary>Beschriftung des Speichern-Buttons.</summary>
    public const string ButtonSave = "Settings.Button.Save";

    /// <summary>Beschriftung des Abbrechen-Buttons.</summary>
    public const string ButtonCancel = "Settings.Button.Cancel";

    /// <summary>Beschriftung des Anwenden-Buttons.</summary>
    public const string ButtonApply = "Settings.Button.Apply";

    /// <summary>
    /// Liefert den Anzeigenamen-Schluessel eines Pausenmodells.
    /// </summary>
    /// <param name="model">Pausenmodell.</param>
    /// <returns>Schluessel der Form <c>"Settings.Model.&lt;Name&gt;"</c>.</returns>
    public static string ForModel(BreakModel model) => "Settings.Model." + model;

    /// <summary>
    /// Liefert den Anzeigenamen-Schluessel eines Erinnerungstons.
    /// </summary>
    /// <param name="soundType">Ton-Typ.</param>
    /// <returns>Schluessel der Form <c>"Settings.Sound.&lt;Name&gt;"</c>.</returns>
    public static string ForSound(SoundType soundType) => "Settings.Sound." + soundType;

    /// <summary>
    /// Liefert den Anzeigenamen-Schluessel einer Sprache.
    /// </summary>
    /// <param name="language">Sprache.</param>
    /// <returns>Schluessel der Form <c>"Language.&lt;Name&gt;"</c>.</returns>
    public static string ForLanguage(Language language) => "Language." + language;
}
