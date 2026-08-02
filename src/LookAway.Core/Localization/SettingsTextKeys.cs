using LookAway.Core.Enums;

namespace LookAway.Core.Localization;

/// <summary>
/// Sprachneutrale Schlüssel der Settings-Oberfläche. Die zugehörigen Texte
/// liegen in den eingebetteten Sprachtabellen der Data-Schicht.
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

    /// <summary>Tab "Über LookAway".</summary>
    public const string TabAbout = "Settings.Tab.About";

    /// <summary>Tab "Sound".</summary>
    public const string TabSound = "Settings.Tab.Sound";

    /// <summary>Tab "Statistik".</summary>
    public const string TabStatistics = "Settings.Tab.Statistics";

    /// <summary>Statistik-Abschnitt "Heute".</summary>
    public const string StatisticsToday = "Statistics.Today";

    /// <summary>Statistik-Abschnitt "Diese Woche".</summary>
    public const string StatisticsWeek = "Statistics.Week";

    /// <summary>Statistik-Abschnitt "Dieses Jahr".</summary>
    public const string StatisticsYear = "Statistics.Year";

    /// <summary>Statistik-Beschriftung "Pausen".</summary>
    public const string StatisticsBreaks = "Statistics.Breaks";

    /// <summary>Statistik-Beschriftung "Pausenzeit".</summary>
    public const string StatisticsBreakTime = "Statistics.BreakTime";

    /// <summary>Statistik-Beschriftung "Übersprungen".</summary>
    public const string StatisticsSkipped = "Statistics.Skipped";

    /// <summary>Statistik-Export-Button.</summary>
    public const string StatisticsExport = "Statistics.Export";

    /// <summary>Tab "Hotkeys".</summary>
    public const string TabHotkeys = "Settings.Tab.Hotkeys";

    /// <summary>Beschriftung "Globale Hotkeys aktivieren".</summary>
    public const string HotkeysEnableLabel = "Settings.Hotkeys.EnableLabel";

    /// <summary>Beschriftung der Aktion "Pause starten".</summary>
    public const string HotkeyStartBreak = "Settings.Hotkeys.StartBreak";

    /// <summary>Beschriftung der Aktion "Überspringen/Snooze".</summary>
    public const string HotkeySkipOrSnooze = "Settings.Hotkeys.SkipOrSnooze";

    /// <summary>Beschriftung der Aktion "DND umschalten".</summary>
    public const string HotkeyToggleDnd = "Settings.Hotkeys.ToggleDnd";

    /// <summary>Beschriftung des Zurücksetzen-Buttons.</summary>
    public const string HotkeysReset = "Settings.Hotkeys.Reset";

    /// <summary>Beschriftung der Schaltfläche "Neu belegen".</summary>
    public const string HotkeysCapture = "Settings.Hotkeys.Capture";

    /// <summary>Aufforderung während der Aufnahme.</summary>
    public const string HotkeysCapturePrompt = "Settings.Hotkeys.CapturePrompt";

    /// <summary>Rückmeldung: Kombination übernommen.</summary>
    public const string HotkeysCaptureAssigned = "Settings.Hotkeys.CaptureAssigned";

    /// <summary>Rückmeldung: Kombination ohne Strg/Alt/Win abgelehnt.</summary>
    public const string HotkeysCaptureInvalid = "Settings.Hotkeys.CaptureInvalid";

    /// <summary>Rückmeldung: Kombination ist bereits belegt.</summary>
    public const string HotkeysCaptureTaken = "Settings.Hotkeys.CaptureTaken";

    /// <summary>Rückmeldung: Aufnahme abgebrochen.</summary>
    public const string HotkeysCaptureCancelled = "Settings.Hotkeys.CaptureCancelled";

    /// <summary>Beschriftung "Auf Updates prüfen".</summary>
    public const string UpdateEnableLabel = "Settings.Update.EnableLabel";

    /// <summary>Beschriftung "Automatisch aktualisieren".</summary>
    public const string UpdateAutoLabel = "Settings.Update.AutoLabel";

    /// <summary>Hinweistext zur Auto-Update-Option.</summary>
    public const string UpdateAutoHint = "Settings.Update.AutoHint";

    /// <summary>Beschriftung der Prüf-Häufigkeit.</summary>
    public const string UpdateFrequencyLabel = "Settings.Update.FrequencyLabel";

    /// <summary>Beschriftung des "Jetzt prüfen"-Buttons.</summary>
    public const string UpdateCheckNow = "Settings.Update.CheckNow";

    /// <summary>Status "auf dem neuesten Stand".</summary>
    public const string UpdateUpToDate = "Settings.Update.UpToDate";

    /// <summary>Status "Update verfügbar" (Formatstring {0}).</summary>
    public const string UpdateAvailable = "Settings.Update.Available";

    /// <summary>Status "wird geprüft".</summary>
    public const string UpdateChecking = "Settings.Update.Checking";

    /// <summary>Download-Link-Text.</summary>
    public const string UpdateDownload = "Settings.Update.Download";

    /// <summary>Beschriftung des "Jetzt installieren"-Buttons.</summary>
    public const string UpdateInstallNow = "Settings.Update.InstallNow";

    /// <summary>Status "wird heruntergeladen/vorbereitet".</summary>
    public const string UpdateStaging = "Settings.Update.Staging";

    /// <summary>Status "bereit, wird beim Neustart installiert".</summary>
    public const string UpdateReady = "Settings.Update.Ready";

    /// <summary>Status "Installation fehlgeschlagen".</summary>
    public const string UpdateInstallFailed = "Settings.Update.InstallFailed";

    /// <summary>Tab "Pause-Aktionen".</summary>
    public const string TabPauseActions = "Settings.Tab.PauseActions";

    /// <summary>Beschriftung "Bildschirm dimmen".</summary>
    public const string PauseActionsDimEnable = "Settings.PauseActions.DimEnable";

    /// <summary>Beschriftung der Pause-Helligkeit.</summary>
    public const string PauseActionsDimBrightness = "Settings.PauseActions.DimBrightness";

    /// <summary>Beschriftung "Medien pausieren".</summary>
    public const string PauseActionsPauseMedia = "Settings.PauseActions.PauseMedia";

    /// <summary>Beschriftung "Medien fortsetzen".</summary>
    public const string PauseActionsResumeMedia = "Settings.PauseActions.ResumeMedia";

    /// <summary>Hinweistext, welche Player automatisch pausiert werden.</summary>
    public const string PauseActionsPauseMediaHint = "Settings.PauseActions.PauseMediaHint";

    /// <summary>Beschriftung "Alle Bildschirme abdunkeln".</summary>
    public const string PauseActionsDarkenAllScreens = "Settings.PauseActions.DarkenAllScreens";

    /// <summary>Beschriftung des Overlay-Farbwählers.</summary>
    public const string PauseActionsOverlayColor = "Settings.PauseActions.OverlayColor";

    /// <summary>Hinweistext zum Overlay-Farbwähler (Transparenz).</summary>
    public const string PauseActionsOverlayColorHint = "Settings.PauseActions.OverlayColorHint";

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel einer Prüf-Häufigkeit.
    /// </summary>
    /// <param name="frequency">Häufigkeit.</param>
    /// <returns>Schlüssel der Form <c>"Settings.Update.Frequency.&lt;Name&gt;"</c>.</returns>
    public static string ForFrequency(UpdateCheckFrequency frequency) => "Settings.Update.Frequency." + frequency;

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
