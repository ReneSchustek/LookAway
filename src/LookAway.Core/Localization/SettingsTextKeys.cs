using LookAway.Core.Enums;

namespace LookAway.Core.Localization;

/// <summary>
/// Sprachneutrale Schlüssel der Settings-Oberfläche. Die zugehörigen Texte
/// liegen in den eingebetteten Sprachtabellen der Data-Schicht.
/// </summary>
/// <remarks>
/// Nach Seiten verteilt: Rahmen, Allgemein, Modell, Intervalle, Ton und Über stehen hier, die
/// Listenseiten in <c>SettingsTextKeys.Lists.cs</c>, Statistik, Hotkeys, Update und Pausen-Aktionen
/// in <c>SettingsTextKeys.Pages.cs</c>.
/// </remarks>
public static partial class SettingsTextKeys
{
    /// <summary>Fenstertitel.</summary>
    public const string Title = "Settings.Title";

    /// <summary>Tab "Allgemein".</summary>
    public const string TabGeneral = "Settings.Tab.General";

    /// <summary>Tab "Pausenmodell".</summary>
    public const string TabModel = "Settings.Tab.Model";

    /// <summary>Tab "Eigene Intervalle".</summary>
    public const string TabIntervals = "Settings.Tab.Intervals";

    /// <summary>Tab "Über LookAway".</summary>
    public const string TabAbout = "Settings.Tab.About";

    /// <summary>Tab "Sound".</summary>
    public const string TabSound = "Settings.Tab.Sound";

    /// <summary>Tab "Protokoll".</summary>
    public const string TabLog = "Settings.Tab.Log";

    /// <summary>Beschriftung der Erscheinungsbild-Auswahl.</summary>
    public const string AppearanceLabel = "Settings.Appearance.Label";

    /// <summary>Hinweistext zur Erscheinungsbild-Auswahl.</summary>
    public const string AppearanceHint = "Settings.Appearance.Hint";

    /// <summary>Erklärende Zeile über der Pausenmodell-Liste.</summary>
    public const string ModelSubtitle = "Settings.Model.Subtitle";

    /// <summary>Platzhalter des Suchfelds der Pausenmodelle.</summary>
    public const string ModelSearchPlaceholder = "Settings.Model.SearchPlaceholder";

    /// <summary>Filter-Chip "Alle" der Pausenmodelle.</summary>
    public const string ModelFilterAll = "Settings.Model.FilterAll";

    /// <summary>Filter-Chip "In Verwendung" der Pausenmodelle.</summary>
    public const string ModelFilterActive = "Settings.Model.FilterActive";

    /// <summary>Filter-Chip "Übrige" der Pausenmodelle.</summary>
    public const string ModelFilterInactive = "Settings.Model.FilterInactive";

    /// <summary>Zahl der aufgezeichneten Pausen eines Modells (Formatstring {0}).</summary>
    public const string ModelBreakCount = "Settings.Model.BreakCount";

    /// <summary>Kennzeichnung der gerade verwendeten Modell-Kachel.</summary>
    public const string ModelActiveBadge = "Settings.Model.ActiveBadge";

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel eines Erscheinungsbilds.
    /// </summary>
    /// <param name="theme">Erscheinungsbild.</param>
    /// <returns>Schlüssel der Form <c>"Settings.Appearance.&lt;Name&gt;"</c>.</returns>
    public static string ForTheme(AppTheme theme) => "Settings.Appearance." + theme;

    /// <summary>Beschriftung "Ton abspielen".</summary>
    public const string SoundEnableLabel = "Settings.Sound.EnableLabel";

    /// <summary>Beschriftung der Ton-Auswahl.</summary>
    public const string SoundSelectLabel = "Settings.Sound.SelectLabel";

    /// <summary>Beschriftung der Lautstärke.</summary>
    public const string SoundVolumeLabel = "Settings.Sound.VolumeLabel";

    /// <summary>Beschriftung des Vorhör-Buttons.</summary>
    public const string SoundPreviewButton = "Settings.Sound.PreviewButton";

    /// <summary>Beschriftung des Sprach-Dropdowns.</summary>
    public const string LanguageLabel = "Settings.Language.Label";

    /// <summary>Beschriftung der Autostart-Checkbox.</summary>
    public const string AutoStartLabel = "Settings.AutoStart.Label";

    /// <summary>Beschriftung der Auto-Pause-Checkbox.</summary>
    public const string IdlePauseLabel = "Settings.Idle.PauseLabel";

    /// <summary>Beschriftung der Inaktivitätsschwelle.</summary>
    public const string IdleThresholdLabel = "Settings.Idle.ThresholdLabel";

    /// <summary>Beschriftung der DND-Checkbox.</summary>
    public const string FullscreenSuppressLabel = "Settings.Fullscreen.SuppressLabel";

    /// <summary>Beschriftung der Auto-Pausenstart-Checkbox.</summary>
    public const string AutoStartBreakLabel = "Settings.AutoStartBreak.Label";

    /// <summary>Beschriftung der Auto-Pausenstart-Verzögerung.</summary>
    public const string AutoStartBreakSecondsLabel = "Settings.AutoStartBreak.SecondsLabel";

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

    /// <summary>Hinweistext über dem freiwilligen Spenden-Eintrag.</summary>
    public const string SupportHeading = "Settings.About.SupportHeading";

    /// <summary>Beschriftung des Spenden-Links.</summary>
    public const string SupportLabel = "Settings.About.SupportLabel";

    /// <summary>Beschriftung des Speichern-Buttons.</summary>
    public const string ButtonSave = "Settings.Button.Save";

    /// <summary>Beschriftung des Abbrechen-Buttons.</summary>
    public const string ButtonCancel = "Settings.Button.Cancel";

    /// <summary>Beschriftung des Anwenden-Buttons.</summary>
    public const string ButtonApply = "Settings.Button.Apply";

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel eines Pausenmodells.
    /// </summary>
    /// <param name="model">Pausenmodell.</param>
    /// <returns>Schlüssel der Form <c>"Settings.Model.&lt;Name&gt;"</c>.</returns>
    public static string ForModel(BreakModel model) => "Settings.Model." + model;

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel eines Erinnerungstons.
    /// </summary>
    /// <param name="soundType">Ton-Typ.</param>
    /// <returns>Schlüssel der Form <c>"Settings.Sound.&lt;Name&gt;"</c>.</returns>
    public static string ForSound(SoundType soundType) => "Settings.Sound." + soundType;

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel einer Sprache.
    /// </summary>
    /// <param name="language">Sprache.</param>
    /// <returns>Schlüssel der Form <c>"Language.&lt;Name&gt;"</c>.</returns>
    public static string ForLanguage(Language language) => "Language." + language;
}
