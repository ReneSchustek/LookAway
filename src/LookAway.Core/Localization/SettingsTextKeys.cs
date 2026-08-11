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

    /// <summary>Tab "Aufgaben".</summary>
    public const string TabTasks = "Settings.Tab.Tasks";

    /// <summary>Erklärende Zeile über der Aufgabenliste.</summary>
    public const string TasksSubtitle = "Settings.Tasks.Subtitle";

    /// <summary>Platzhalter des Suchfelds der Aufgaben.</summary>
    public const string TasksSearchPlaceholder = "Settings.Tasks.SearchPlaceholder";

    /// <summary>Platzhalter des Eingabefelds für eine neue Aufgabe.</summary>
    public const string TasksNewPlaceholder = "Settings.Tasks.NewPlaceholder";

    /// <summary>Beschriftung der Schaltfläche zum Anlegen.</summary>
    public const string TasksAdd = "Settings.Tasks.Add";

    /// <summary>Filter-Chip "Alle" der Aufgaben.</summary>
    public const string TasksFilterAll = "Settings.Tasks.FilterAll";

    /// <summary>Filter-Chip "Offen".</summary>
    public const string TasksFilterOpen = "Settings.Tasks.FilterOpen";

    /// <summary>Filter-Chip "Erledigt".</summary>
    public const string TasksFilterCompleted = "Settings.Tasks.FilterCompleted";

    /// <summary>Beschriftung "Umbenennen".</summary>
    public const string TasksRename = "Settings.Tasks.Rename";

    /// <summary>Beschriftung "Löschen".</summary>
    public const string TasksDelete = "Settings.Tasks.Delete";

    /// <summary>Beschriftung "Übernehmen".</summary>
    public const string TasksCommit = "Settings.Tasks.Commit";

    /// <summary>Beschriftung "Abbrechen" beim Umbenennen.</summary>
    public const string TasksCancel = "Settings.Tasks.Cancel";

    /// <summary>Rückfrage vor dem Löschen.</summary>
    public const string TasksDeleteConfirm = "Settings.Tasks.DeleteConfirm";

    /// <summary>Zahl der Pausen einer Aufgabe (Formatstring {0}).</summary>
    public const string TasksBreakCount = "Settings.Tasks.BreakCount";

    /// <summary>Überschrift des Leerzustands "noch keine Aufgabe".</summary>
    public const string TasksEmptyTitle = "Settings.Tasks.EmptyTitle";

    /// <summary>Erklärung des Leerzustands "noch keine Aufgabe".</summary>
    public const string TasksEmptyText = "Settings.Tasks.EmptyText";

    /// <summary>Erklärende Zeile über dem Protokoll.</summary>
    public const string LogSubtitle = "Settings.Log.Subtitle";

    /// <summary>Platzhalter des Suchfelds im Protokoll.</summary>
    public const string LogSearchPlaceholder = "Settings.Log.SearchPlaceholder";

    /// <summary>Filter-Chip "Alle Stufen".</summary>
    public const string LogLevelAll = "Settings.Log.LevelAll";

    /// <summary>Filter-Chip "Hinweise".</summary>
    public const string LogLevelInformation = "Settings.Log.LevelInformation";

    /// <summary>Filter-Chip "Warnungen".</summary>
    public const string LogLevelWarning = "Settings.Log.LevelWarning";

    /// <summary>Filter-Chip "Fehler".</summary>
    public const string LogLevelError = "Settings.Log.LevelError";

    /// <summary>Stufe eines einzelnen Eintrags: Hinweis (Einzahl, anders als der Chip).</summary>
    public const string LogEntryInformation = "Settings.Log.EntryInformation";

    /// <summary>Stufe eines einzelnen Eintrags: Warnung.</summary>
    public const string LogEntryWarning = "Settings.Log.EntryWarning";

    /// <summary>Stufe eines einzelnen Eintrags: Fehler.</summary>
    public const string LogEntryError = "Settings.Log.EntryError";

    /// <summary>Filter-Chip "Gesamter Zeitraum".</summary>
    public const string LogPeriodAll = "Settings.Log.PeriodAll";

    /// <summary>Filter-Chip "Heute".</summary>
    public const string LogPeriodToday = "Settings.Log.PeriodToday";

    /// <summary>Filter-Chip "Letzte 7 Tage".</summary>
    public const string LogPeriodWeek = "Settings.Log.PeriodWeek";

    /// <summary>Überschrift des Leerzustands "noch nichts protokolliert".</summary>
    public const string LogEmptyTitle = "Settings.Log.EmptyTitle";

    /// <summary>Erklärung des Leerzustands "noch nichts protokolliert".</summary>
    public const string LogEmptyText = "Settings.Log.EmptyText";

    /// <summary>Beschriftung der Schaltfläche "Neu laden".</summary>
    public const string LogReload = "Settings.Log.Reload";

    /// <summary>Überschrift des Leerzustands "nichts gefunden".</summary>
    public const string NoResultsTitle = "Common.NoResultsTitle";

    /// <summary>Erklärung des Leerzustands "nichts gefunden".</summary>
    public const string NoResultsText = "Common.NoResultsText";

    /// <summary>Beschriftung der Schaltfläche "Suche zurücksetzen".</summary>
    public const string ResetSearch = "Common.ResetSearch";

    /// <summary>Beschriftung des Löschen-Zeichens im Suchfeld.</summary>
    public const string ClearSearch = "Common.ClearSearch";

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel eines Erscheinungsbilds.
    /// </summary>
    /// <param name="theme">Erscheinungsbild.</param>
    /// <returns>Schlüssel der Form <c>"Settings.Appearance.&lt;Name&gt;"</c>.</returns>
    public static string ForTheme(AppTheme theme) => "Settings.Appearance." + theme;

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
