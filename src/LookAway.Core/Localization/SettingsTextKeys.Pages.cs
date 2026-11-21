using LookAway.Core.Enums;

namespace LookAway.Core.Localization;

/// <summary>
/// Schlüssel der Seiten Statistik, Hotkeys, Update und Pausen-Aktionen.
/// </summary>
public static partial class SettingsTextKeys
{
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

    /// <summary>Rückmeldung nach gespeichertem CSV-Export.</summary>
    public const string StatisticsExportSaved = "Statistics.ExportSaved";

    /// <summary>Rückmeldung nach gescheitertem CSV-Export.</summary>
    public const string StatisticsExportFailed = "Statistics.ExportFailed";

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

    /// <summary>Hinweistext zur Auto-Update-Option bei einer installierten Kopie (Setup).</summary>
    public const string UpdateAutoHintSetup = "Settings.Update.AutoHintSetup";

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

    /// <summary>Status, nachdem das Setup einer Aktualisierung gestartet wurde.</summary>
    public const string UpdateSetupStarted = "Settings.Update.SetupStarted";

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

    /// <summary>Hinweistext zum Overlay-Farbwähler; nennt die Farbe auch als Ersatz für ein Bild.</summary>
    public const string PauseActionsOverlayColorHint = "Settings.PauseActions.OverlayColorHint";

    /// <summary>Überschrift und Beschriftung der Wahl zwischen Farbe und Bild.</summary>
    public const string PauseActionsOverlayBackground = "Settings.PauseActions.OverlayBackground";

    /// <summary>Hinweistext zum Overlay-Bild: Formate, Füllen und Rückfall auf die Farbe.</summary>
    public const string PauseActionsOverlayImageHint = "Settings.PauseActions.OverlayImageHint";

    /// <summary>Beschriftung des Knopfs, der die Bildauswahl öffnet.</summary>
    public const string PauseActionsOverlayImageChoose = "Settings.PauseActions.OverlayImageChoose";

    /// <summary>Anzeige, solange kein Bild gewählt ist.</summary>
    public const string PauseActionsOverlayImageNone = "Settings.PauseActions.OverlayImageNone";

    /// <summary>Beschriftung des Reglers für die Abdunklung über dem Bild.</summary>
    public const string PauseActionsOverlayImageDim = "Settings.PauseActions.OverlayImageDim";

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel einer Art des Overlay-Hintergrunds.
    /// </summary>
    /// <param name="background">Art des Hintergrunds.</param>
    /// <returns>Schlüssel der Form <c>"Settings.PauseActions.OverlayBackground.&lt;Name&gt;"</c>.</returns>
    public static string ForOverlayBackground(OverlayBackground background) => "Settings.PauseActions.OverlayBackground." + background;

    /// <summary>
    /// Liefert den Anzeigenamen-Schlüssel einer Prüf-Häufigkeit.
    /// </summary>
    /// <param name="frequency">Häufigkeit.</param>
    /// <returns>Schlüssel der Form <c>"Settings.Update.Frequency.&lt;Name&gt;"</c>.</returns>
    public static string ForFrequency(UpdateCheckFrequency frequency) => "Settings.Update.Frequency." + frequency;
}
