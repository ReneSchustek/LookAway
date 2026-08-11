using CommunityToolkit.Mvvm.ComponentModel;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Die Beschriftungen des Einstellungsfensters.
/// </summary>
/// <remarks>
/// Sie standen als rund vierzig Eigenschaften im <see cref="SettingsViewModel"/> und
/// machten dort den größten Teil der Klasse aus, ohne zu ihrer Aufgabe zu gehören:
/// Das ViewModel lädt, prüft und speichert Einstellungen — Beschriftungen nachzuschlagen
/// ist eine andere Verantwortung. Als eigenes Objekt komponiert statt vererbt, damit
/// beide unabhängig bleiben.
///
/// Nach einem Sprachwechsel ruft das ViewModel <see cref="Refresh"/>; alle gebundenen
/// Texte werden dann neu gelesen.
/// </remarks>
internal sealed class SettingsTexts : ObservableObject
{
    private readonly ILocalizationService _localization;

    /// <summary>Erzeugt die Beschriftungen.</summary>
    /// <param name="localization">Quelle der übersetzten Texte.</param>
    public SettingsTexts(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
    }

    /// <summary>Fenstertitel.</summary>
    public string Title => _localization.GetText(SettingsTextKeys.Title);

    /// <summary>Tab-Überschrift "Allgemein".</summary>
    public string TabGeneralHeader => _localization.GetText(SettingsTextKeys.TabGeneral);

    /// <summary>Tab-Überschrift "Pausenmodell".</summary>
    public string TabModelHeader => _localization.GetText(SettingsTextKeys.TabModel);

    /// <summary>Tab-Überschrift "Eigene Intervalle".</summary>
    public string TabIntervalsHeader => _localization.GetText(SettingsTextKeys.TabIntervals);

    /// <summary>Tab-Überschrift "Über LookAway".</summary>
    public string TabAboutHeader => _localization.GetText(SettingsTextKeys.TabAbout);

    /// <summary>Tab-Überschrift "Protokoll".</summary>
    public string TabLogHeader => _localization.GetText(SettingsTextKeys.TabLog);

    /// <summary>Tab-Überschrift "Aufgaben".</summary>
    public string TabTasksHeader => _localization.GetText(SettingsTextKeys.TabTasks);

    /// <summary>Beschriftung der Erscheinungsbild-Auswahl.</summary>
    public string AppearanceLabel => _localization.GetText(SettingsTextKeys.AppearanceLabel);

    /// <summary>Hinweistext zur Erscheinungsbild-Auswahl.</summary>
    public string AppearanceHint => _localization.GetText(SettingsTextKeys.AppearanceHint);

    /// <summary>Beschriftung des Löschen-Zeichens in den Suchfeldern.</summary>
    public string ClearSearchLabel => _localization.GetText(SettingsTextKeys.ClearSearch);

    /// <summary>Beschriftung der Sprachauswahl.</summary>
    public string LanguageLabel => _localization.GetText(SettingsTextKeys.LanguageLabel);

    /// <summary>Beschriftung der Autostart-Option.</summary>
    public string AutoStartLabel => _localization.GetText(SettingsTextKeys.AutoStartLabel);

    /// <summary>Beschriftung der Auto-Pause-Option.</summary>
    public string IdlePauseLabel => _localization.GetText(SettingsTextKeys.IdlePauseLabel);

    /// <summary>Beschriftung der Inaktivitätsschwelle.</summary>
    public string IdleThresholdLabel => _localization.GetText(SettingsTextKeys.IdleThresholdLabel);

    /// <summary>Beschriftung der DND-Option.</summary>
    public string FullscreenSuppressLabel => _localization.GetText(SettingsTextKeys.FullscreenSuppressLabel);

    /// <summary>Beschriftung der Auto-Pausenstart-Option.</summary>
    public string AutoStartBreakLabel => _localization.GetText(SettingsTextKeys.AutoStartBreakLabel);

    /// <summary>Beschriftung der Auto-Pausenstart-Verzögerung.</summary>
    public string AutoStartBreakSecondsLabel => _localization.GetText(SettingsTextKeys.AutoStartBreakSecondsLabel);

    /// <summary>Beschriftung der Modellauswahl.</summary>
    public string ModelLabel => _localization.GetText(SettingsTextKeys.ModelLabel);

    /// <summary>Beschriftung des "Eigene Dauern verwenden"-Schalters.</summary>
    public string UseCustomLabel => _localization.GetText(SettingsTextKeys.IntervalsUseCustom);

    /// <summary>Hinweistext im Intervall-Bereich.</summary>
    public string IntervalsHint => _localization.GetText(SettingsTextKeys.IntervalsHint);

    /// <summary>Beschriftung der Arbeitsdauer.</summary>
    public string WorkLabel => _localization.GetText(SettingsTextKeys.WorkLabel);

    /// <summary>Beschriftung der Pausendauer.</summary>
    public string BreakLabel => _localization.GetText(SettingsTextKeys.BreakLabel);

    /// <summary>Beschriftung "Version".</summary>
    public string AboutVersionLabel => _localization.GetText(SettingsTextKeys.AboutVersionLabel);

    /// <summary>Beschriftung "Lizenz".</summary>
    public string LicenseLabel => _localization.GetText(SettingsTextKeys.LicenseLabel);

    /// <summary>Lizenztext.</summary>
    public string LicenseText => _localization.GetText(SettingsTextKeys.License);

    /// <summary>Beschriftung "Dokumentation".</summary>
    public string DocsLabel => _localization.GetText(SettingsTextKeys.DocsLabel);

    /// <summary>Hinweistext über dem freiwilligen Spenden-Eintrag.</summary>
    public string SupportHeading => _localization.GetText(SettingsTextKeys.SupportHeading);

    /// <summary>Beschriftung des Spenden-Links.</summary>
    public string SupportLabel => _localization.GetText(SettingsTextKeys.SupportLabel);

    /// <summary>Beschriftung des Speichern-Buttons.</summary>
    public string SaveLabel => _localization.GetText(SettingsTextKeys.ButtonSave);

    /// <summary>Beschriftung des Abbrechen-Buttons.</summary>
    public string CancelLabel => _localization.GetText(SettingsTextKeys.ButtonCancel);

    /// <summary>Beschriftung des Anwenden-Buttons.</summary>
    public string ApplyLabel => _localization.GetText(SettingsTextKeys.ButtonApply);

    /// <summary>Tab-Überschrift "Sound".</summary>
    public string TabSoundHeader => _localization.GetText(SettingsTextKeys.TabSound);

    /// <summary>Beschriftung der Ton-aktivieren-Option.</summary>
    public string SoundEnableLabel => _localization.GetText(SettingsTextKeys.SoundEnableLabel);

    /// <summary>Beschriftung der Ton-Auswahl.</summary>
    public string SoundSelectLabel => _localization.GetText(SettingsTextKeys.SoundSelectLabel);

    /// <summary>Beschriftung der Lautstärke.</summary>
    public string SoundVolumeLabel => _localization.GetText(SettingsTextKeys.SoundVolumeLabel);

    /// <summary>Beschriftung des Vorhör-Buttons.</summary>
    public string SoundPreviewLabel => _localization.GetText(SettingsTextKeys.SoundPreviewButton);

    /// <summary>Tab-Überschrift "Pause-Aktionen".</summary>
    public string TabPauseActionsHeader => _localization.GetText(SettingsTextKeys.TabPauseActions);

    /// <summary>Beschriftung "Bildschirm dimmen".</summary>
    public string DimEnableLabel => _localization.GetText(SettingsTextKeys.PauseActionsDimEnable);

    /// <summary>Beschriftung der Pause-Helligkeit.</summary>
    public string DimBrightnessLabel => _localization.GetText(SettingsTextKeys.PauseActionsDimBrightness);

    /// <summary>Beschriftung "Medien pausieren".</summary>
    public string PauseMediaLabel => _localization.GetText(SettingsTextKeys.PauseActionsPauseMedia);

    /// <summary>Beschriftung "Medien fortsetzen".</summary>
    public string ResumeMediaLabel => _localization.GetText(SettingsTextKeys.PauseActionsResumeMedia);

    /// <summary>Hinweistext, welche Player automatisch pausiert werden.</summary>
    public string PauseMediaHint => _localization.GetText(SettingsTextKeys.PauseActionsPauseMediaHint);

    /// <summary>Beschriftung "Alle Bildschirme abdunkeln".</summary>
    public string DarkenAllScreensLabel => _localization.GetText(SettingsTextKeys.PauseActionsDarkenAllScreens);

    /// <summary>Beschriftung des Overlay-Farbwählers.</summary>
    public string OverlayColorLabel => _localization.GetText(SettingsTextKeys.PauseActionsOverlayColor);

    /// <summary>Hinweistext zum Overlay-Farbwähler (Transparenz).</summary>
    public string OverlayColorHint => _localization.GetText(SettingsTextKeys.PauseActionsOverlayColorHint);

    /// <summary>
    /// Liest alle Beschriftungen neu — nach einem Sprachwechsel aufzurufen.
    /// </summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}
