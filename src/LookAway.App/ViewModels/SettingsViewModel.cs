using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.App.Services;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.Core.Services;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.App.ViewModels;

/// <summary>
/// Zustand und Aktionslogik des Settings-Fensters — UI-frei und damit ohne WinUI
/// testbar. Kapselt Laden, Validieren und Persistieren der Benutzerkonfiguration
/// und hält den Autostart-Eintrag (über den <see cref="AutoStartCoordinator"/>)
/// synchron.
/// </summary>
/// <remarks>
/// Sprachwechsel wirken sofort: das Setzen der Auswahl schaltet die
/// <see cref="ILocalizationService"/>-Sprache um, woraufhin alle gebundenen Texte
/// aktualisiert werden. "Abbrechen" stellt die zuletzt gespeicherte Sprache wieder her.
/// </remarks>
internal sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly AutoStartCoordinator _autoStartCoordinator;
    private readonly ILocalizationService _localization;
    private readonly ISoundService _soundService;
    private readonly IUpdateChecker _updateChecker;
    private readonly IUpdateInstaller _updateInstaller;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly string _applicationVersion;

    /// <summary>Statistik-Bereich, als eigenes ViewModel komponiert.</summary>
    public StatisticsViewModel Statistics { get; }

    /// <summary>Pausenmodell-Liste mit Suche und Filter, als eigenes ViewModel komponiert.</summary>
    public BreakModelListViewModel BreakModels { get; }

    /// <summary>Protokoll-Ansicht mit Suche und Filter, als eigenes ViewModel komponiert.</summary>
    public LogViewModel Log { get; }

    /// <summary>Aufgabenliste mit Suche und Filter, als eigenes ViewModel komponiert.</summary>
    public WorkTaskListViewModel Tasks { get; }

    private Language _originalLanguage;
    private bool _isInitializing;
    private bool _disposed;

    [ObservableProperty]
    public partial SettingsOption<Language>? SelectedLanguageOption { get; set; }

    [ObservableProperty]
    public partial SettingsOption<BreakModel>? SelectedModelOption { get; set; }

    [ObservableProperty]
    public partial SettingsOption<AppTheme>? SelectedThemeOption { get; set; }

    [ObservableProperty]
    public partial bool AutoStart { get; set; }

    [ObservableProperty]
    public partial bool UseCustomDurations { get; set; }

    [ObservableProperty]
    public partial int WorkMinutes { get; set; }

    [ObservableProperty]
    public partial int BreakMinutes { get; set; }

    [ObservableProperty]
    public partial bool PauseOnIdle { get; set; }

    [ObservableProperty]
    public partial int IdleThresholdMinutes { get; set; }

    [ObservableProperty]
    public partial bool SuppressOnFullscreen { get; set; }

    [ObservableProperty]
    public partial bool AutoStartBreakEnabled { get; set; }

    [ObservableProperty]
    public partial int AutoStartBreakSeconds { get; set; }

    [ObservableProperty]
    public partial string? WorkError { get; set; }

    [ObservableProperty]
    public partial string? BreakError { get; set; }

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; }

    [ObservableProperty]
    public partial SettingsOption<SoundType>? SelectedSoundOption { get; set; }

    [ObservableProperty]
    public partial int SoundVolume { get; set; }

    [ObservableProperty]
    public partial bool HotkeysEnabled { get; set; }

    [ObservableProperty]
    public partial bool DimScreenDuringBreak { get; set; }

    [ObservableProperty]
    public partial int DimBrightnessPercent { get; set; }

    [ObservableProperty]
    public partial bool PauseMediaDuringBreak { get; set; }

    [ObservableProperty]
    public partial bool ResumeMediaAfterBreak { get; set; }

    [ObservableProperty]
    public partial bool DarkenAllScreens { get; set; }

    [ObservableProperty]
    public partial string BreakOverlayColor { get; set; }

    /// <summary>
    /// Erzeugt das ViewModel mit seinen Abhängigkeiten.
    /// </summary>
    /// <param name="settingsRepository">Persistenz der Einstellungen.</param>
    /// <param name="autoStartCoordinator">Hält Einstellung und Registry synchron.</param>
    /// <param name="localization">Liefert Texte und steuert den Sprachwechsel.</param>
    /// <param name="soundService">Spielt den Erinnerungston für die Vorschau.</param>
    /// <param name="updateChecker">Prüft auf Updates.</param>
    /// <param name="updateInstaller">Lädt und stellt ein gefundenes Update bereit (Ein-Klick-Installation).</param>
    /// <param name="statistics">Statistik-ViewModel (komponiert).</param>
    /// <param name="breakModels">Pausenmodell-Liste (komponiert).</param>
    /// <param name="log">Protokoll-Ansicht (komponiert).</param>
    /// <param name="tasks">Aufgabenliste (komponiert).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="applicationVersion">Anzuzeigende Versionsnummer (Über-Bereich).</param>
    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        AutoStartCoordinator autoStartCoordinator,
        ILocalizationService localization,
        ISoundService soundService,
        IUpdateChecker updateChecker,
        IUpdateInstaller updateInstaller,
        StatisticsViewModel statistics,
        BreakModelListViewModel breakModels,
        LogViewModel log,
        WorkTaskListViewModel tasks,
        ILogger<SettingsViewModel> logger,
        string applicationVersion)
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(autoStartCoordinator);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(soundService);
        ArgumentNullException.ThrowIfNull(updateChecker);
        ArgumentNullException.ThrowIfNull(updateInstaller);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(breakModels);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);

        _settingsRepository = settingsRepository;
        _autoStartCoordinator = autoStartCoordinator;
        _localization = localization;
        _soundService = soundService;
        _updateChecker = updateChecker;
        _updateInstaller = updateInstaller;
        Statistics = statistics;
        BreakModels = breakModels;
        Log = log;
        Tasks = tasks;
        _logger = logger;
        _applicationVersion = applicationVersion;

        // Die Kachel-Auswahl und das Auswahlfeld führen dieselbe Einstellung. Damit
        // es dabei nur eine Wahrheit gibt, meldet die Liste ihre Wahl hierher und
        // bekommt umgekehrt die Marke gesetzt, wenn sie anderswo umgestellt wird.
        BreakModels.ModelSelected += OnBreakModelSelected;

        // Startwerte der gebundenen Eigenschaften; partielle Properties erlauben
        // keine Feld-Initialisierer.
        BreakOverlayColor = Settings.DefaultBreakOverlayColor;
        UpdateStatusText = string.Empty;

        Languages = BuildLanguageOptions();
        Models = BuildModelOptions();
        Sounds = BuildSoundOptions();
        Appearances = BuildThemeOptions();
        UpdateFrequencies = BuildFrequencyOptions();

        _localization.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Wird ausgelöst, wenn das Fenster geschlossen werden soll.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Wird nach erfolgreichem Speichern/Anwenden mit den gültigen Einstellungen
    /// ausgelöst, damit laufende Dienste (Timer, Idle-/Vollbild-Erkennung) sie
    /// sofort übernehmen können.
    /// </summary>
    public event EventHandler<SettingsAppliedEventArgs>? SettingsApplied;

    /// <summary>Auswählbare Sprachen mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<Language>> Languages { get; }

    /// <summary>Auswählbare Pausenmodelle mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<BreakModel>> Models { get; }

    /// <summary>Auswählbare Erinnerungstöne mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<SoundType>> Sounds { get; }

    /// <summary>Auswählbare Erscheinungsbilder mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<AppTheme>> Appearances { get; }

    /// <summary>Untere Grenze der Arbeitsdauer (Minuten) für das aktive Modell.</summary>
    public int WorkMinMinutes { get; private set; } = (int)BreakInterval.MinWorkDuration.TotalMinutes;

    /// <summary>Obere Grenze der Arbeitsdauer (Minuten) für das aktive Modell.</summary>
    public int WorkMaxMinutes { get; private set; } = (int)BreakInterval.MaxWorkDuration.TotalMinutes;

    /// <summary>Untere Grenze der Pausendauer (Minuten).</summary>
    public int BreakMinMinutes => (int)BreakInterval.MinBreakDuration.TotalMinutes;

    /// <summary>Obere Grenze der Pausendauer (Minuten).</summary>
    public int BreakMaxMinutes => (int)BreakInterval.MaxBreakDuration.TotalMinutes;

    /// <summary>Untere Grenze der Inaktivitätsschwelle (Minuten).</summary>
    public int IdleMinMinutes => Settings.MinIdleThresholdMinutes;

    /// <summary>Obere Grenze der Inaktivitätsschwelle (Minuten).</summary>
    public int IdleMaxMinutes => Settings.MaxIdleThresholdMinutes;

    /// <summary>Untere Grenze der Auto-Pausenstart-Verzögerung (Sekunden).</summary>
    public int AutoStartBreakMinSeconds => Settings.MinAutoStartBreakSeconds;

    /// <summary>Obere Grenze der Auto-Pausenstart-Verzögerung (Sekunden).</summary>
    public int AutoStartBreakMaxSeconds => Settings.MaxAutoStartBreakSeconds;

    /// <summary>Schrittweite der Auto-Pausenstart-Verzögerung (Sekunden).</summary>
    public int AutoStartBreakStepSeconds => Settings.AutoStartBreakSecondsStep;

    /// <summary>Wahr, wenn das aktive Modell die Arbeitsdauer nur in einem Bereich erlaubt.</summary>
    public bool HasWorkRange { get; private set; }

    /// <summary>Das aktuell gewählte Pausenmodell.</summary>
    public BreakModel SelectedModel => SelectedModelOption?.Value ?? BreakModel.ClassicPomodoro;

    /// <summary>Die aktuell gewählte Sprache.</summary>
    public Language SelectedLanguage => SelectedLanguageOption?.Value ?? Language.German;

    /// <summary>Der aktuell gewählte Erinnerungston.</summary>
    public SoundType SelectedSound => SelectedSoundOption?.Value ?? SoundType.Chime;

    /// <summary>Das aktuell gewählte Erscheinungsbild.</summary>
    public AppTheme SelectedTheme => SelectedThemeOption?.Value ?? AppTheme.System;

    /// <summary>Untere Grenze der Lautstärke.</summary>
    public int SoundVolumeMin => Settings.MinSoundVolumePercent;

    /// <summary>Obere Grenze der Lautstärke.</summary>
    public int SoundVolumeMax => Settings.MaxSoundVolumePercent;

    /// <summary>Wahr, wenn die aktuellen Eingaben gespeichert werden dürfen.</summary>
    public bool CanPersist => WorkError is null && BreakError is null && HexColor.IsValid(BreakOverlayColor);

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

    /// <summary>Hinweis auf den erlaubten Arbeitsdauer-Bereich (leer, wenn unbeschränkt).</summary>
    public string WorkRangeText => HasWorkRange
        ? string.Format(CultureInfo.CurrentCulture, _localization.GetText(SettingsTextKeys.WorkRangeHint), WorkMinMinutes, WorkMaxMinutes)
        : string.Empty;

    /// <summary>Beschriftung "Version".</summary>
    public string AboutVersionLabel => _localization.GetText(SettingsTextKeys.AboutVersionLabel);

    /// <summary>Anzuzeigende Versionsnummer.</summary>
    public string Version => _applicationVersion;

    /// <summary>Beschriftung "Lizenz".</summary>
    public string LicenseLabel => _localization.GetText(SettingsTextKeys.LicenseLabel);

    /// <summary>Lizenztext.</summary>
    public string LicenseText => _localization.GetText(SettingsTextKeys.License);

    /// <summary>Beschriftung "Dokumentation".</summary>
    public string DocsLabel => _localization.GetText(SettingsTextKeys.DocsLabel);

    /// <summary>URL zur Dokumentation (für einen HyperlinkButton); <c>null</c> bei ungültigem Wert.</summary>
    public Uri? DocsUri =>
        Uri.TryCreate(_localization.GetText(SettingsTextKeys.DocsUrl), UriKind.Absolute, out Uri? uri) ? uri : null;

    /// <summary>Hinweistext über dem freiwilligen Spenden-Eintrag.</summary>
    public string SupportHeading => _localization.GetText(SettingsTextKeys.SupportHeading);

    /// <summary>Beschriftung des Spenden-Links.</summary>
    public string SupportLabel => _localization.GetText(SettingsTextKeys.SupportLabel);

    /// <summary>
    /// Ziel des Spenden-Links. Anders als <see cref="DocsUri"/> stammt die Adresse
    /// <b>nicht</b> aus den Sprachdateien, sondern aus einer Compile-Zeit-Konstante:
    /// Ein zur Laufzeit austauschbares Spendenziel würde fremdes Geld umleiten.
    /// </summary>
    public Uri DonationUri => new(SupportDonation.PayPalUrl);

    /// <summary>
    /// Steuert die Sichtbarkeit des Spenden-Eintrags. Ohne echten Handle bleibt er
    /// verborgen, damit nie ein toter Link auf der Seite steht.
    /// </summary>
    public bool IsSupportVisible => SupportDonation.IsConfigured;

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

    /// <summary>Untere Grenze der Pause-Helligkeit.</summary>
    public int DimBrightnessMin => Settings.MinDimBrightnessPercent;

    /// <summary>Obere Grenze der Pause-Helligkeit.</summary>
    public int DimBrightnessMax => Settings.MaxDimBrightnessPercent;

    /// <summary>
    /// Lädt die persistierten Einstellungen in das ViewModel. Vor dem ersten
    /// Anzeigen aufzurufen.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _isInitializing = true;
        try
        {
            Settings settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(true);

            _originalLanguage = settings.Language;
            _localization.SetLanguage(settings.Language);
            SelectLanguage(settings.Language);
            SelectModel(settings.BreakModel);
            SelectTheme(settings.AppTheme);
            UpdateModelRanges(settings.BreakModel);

            AutoStart = settings.AutoStart;
            PauseOnIdle = settings.PauseOnIdle;
            IdleThresholdMinutes = settings.IdleThresholdMinutes;
            SuppressOnFullscreen = settings.SuppressOnFullscreen;
            AutoStartBreakEnabled = settings.AutoStartBreakEnabled;
            AutoStartBreakSeconds = settings.AutoStartBreakSeconds;

            SoundEnabled = settings.SoundEnabled;
            SelectSound(settings.ReminderSound);
            SoundVolume = settings.SoundVolumePercent;

            HotkeysEnabled = settings.HotkeysEnabled;
            _hotkeyStartBreak = settings.HotkeyStartBreak;
            _hotkeySkipOrSnooze = settings.HotkeySkipOrSnooze;
            _hotkeyToggleDnd = settings.HotkeyToggleDnd;

            UpdateCheckEnabled = settings.UpdateCheckEnabled;
            AutoUpdate = settings.AutoUpdate;
            SelectFrequency(settings.UpdateCheckFrequency);

            DimScreenDuringBreak = settings.DimScreenDuringBreak;
            DimBrightnessPercent = settings.DimBrightnessPercent;
            PauseMediaDuringBreak = settings.PauseMediaDuringBreak;
            ResumeMediaAfterBreak = settings.ResumeMediaAfterBreak;
            DarkenAllScreens = settings.DarkenAllScreens;
            // Overlay-Farbe deckend übernehmen: die Transparenz-/Alpha-Einstellung
            // entfällt (in WinUI 3 nicht als Durchsicht umsetzbar). Eine bisher
            // halbtransparente Farbe wird auf ihr sichtbares, opakes Äquivalent
            // migriert, damit sich das Erscheinungsbild nicht ändert.
            BreakOverlayColor = HexColor.FlattenOverWhite(settings.BreakOverlayColor);

            LoadDurations(settings);
        }
        finally
        {
            _isInitializing = false;
        }

        await Statistics.LoadAsync(cancellationToken).ConfigureAwait(true);
        BreakModels.SetActiveModel(SelectedModel);
        await BreakModels.LoadAsync(cancellationToken).ConfigureAwait(true);
        await Log.LoadAsync(cancellationToken).ConfigureAwait(true);
        await Tasks.LoadAsync(cancellationToken).ConfigureAwait(true);

        Validate();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Wählt das Modell anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="model">Zu wählendes Pausenmodell.</param>
    public void SelectModel(BreakModel model)
        => SelectedModelOption = Models.First(option => option.Value == model);

    /// <summary>Wählt die Sprache anhand ihres Werts (UI/Test-Hilfe).</summary>
    /// <param name="language">Zu wählende Sprache.</param>
    public void SelectLanguage(Language language)
        => SelectedLanguageOption = Languages.First(option => option.Value == language);

    /// <summary>Wählt den Erinnerungston anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="soundType">Zu wählender Ton.</param>
    public void SelectSound(SoundType soundType)
        => SelectedSoundOption = Sounds.First(option => option.Value == soundType);

    /// <summary>Wählt das Erscheinungsbild anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="theme">Zu wählendes Erscheinungsbild.</param>
    public void SelectTheme(AppTheme theme)
        => SelectedThemeOption = Appearances.First(option => option.Value == theme);

    private void LoadDurations(Settings settings)
    {
        if (settings.CustomDurations is { } custom)
        {
            UseCustomDurations = true;
            WorkMinutes = custom.WorkMinutes;
            BreakMinutes = custom.BreakMinutes;
            return;
        }

        UseCustomDurations = false;
        BreakInterval defaults = BreakModelRegistry.GetDefault(settings.BreakModel);
        WorkMinutes = (int)defaults.WorkDuration.TotalMinutes;
        BreakMinutes = (int)defaults.BreakDuration.TotalMinutes;
    }

    [RelayCommand(CanExecute = nameof(CanPersist))]
    private Task SaveAsync() => PersistAsync(closeAfterwards: true);

    [RelayCommand(CanExecute = nameof(CanPersist))]
    private Task ApplyAsync() => PersistAsync(closeAfterwards: false);

    [RelayCommand]
    private void PreviewSound() => _soundService.Play(SelectedSound, SoundVolume);

    [RelayCommand]
    private void Cancel()
    {
        // Nur Vorschau-Änderungen verwerfen: zuletzt gespeicherte Sprache zurück.
        _localization.SetLanguage(_originalLanguage);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task PersistAsync(bool closeAfterwards)
    {
        await ApplyAutoStartAsync().ConfigureAwait(true);

        Settings settings = await _settingsRepository.LoadAsync().ConfigureAwait(true);
        settings.Language = SelectedLanguage;
        settings.BreakModel = SelectedModel;
        settings.AppTheme = SelectedTheme;
        settings.PauseOnIdle = PauseOnIdle;
        settings.IdleThresholdMinutes = Math.Clamp(IdleThresholdMinutes, IdleMinMinutes, IdleMaxMinutes);
        settings.SuppressOnFullscreen = SuppressOnFullscreen;
        settings.AutoStartBreakEnabled = AutoStartBreakEnabled;
        settings.AutoStartBreakSeconds = Math.Clamp(AutoStartBreakSeconds, AutoStartBreakMinSeconds, AutoStartBreakMaxSeconds);
        settings.CustomDurations = UseCustomDurations
            ? new CustomDurations { WorkMinutes = WorkMinutes, BreakMinutes = BreakMinutes }
            : null;
        settings.SoundEnabled = SoundEnabled;
        settings.ReminderSound = SelectedSound;
        settings.SoundVolumePercent = Math.Clamp(SoundVolume, SoundVolumeMin, SoundVolumeMax);
        settings.HotkeysEnabled = HotkeysEnabled;
        settings.HotkeyStartBreak = _hotkeyStartBreak;
        settings.HotkeySkipOrSnooze = _hotkeySkipOrSnooze;
        settings.HotkeyToggleDnd = _hotkeyToggleDnd;
        settings.UpdateCheckEnabled = UpdateCheckEnabled;
        settings.AutoUpdate = AutoUpdate;
        settings.UpdateCheckFrequency = SelectedFrequency;
        settings.DimScreenDuringBreak = DimScreenDuringBreak;
        settings.DimBrightnessPercent = Math.Clamp(DimBrightnessPercent, DimBrightnessMin, DimBrightnessMax);
        settings.PauseMediaDuringBreak = PauseMediaDuringBreak;
        settings.ResumeMediaAfterBreak = ResumeMediaAfterBreak;
        settings.DarkenAllScreens = DarkenAllScreens;
        settings.BreakOverlayColor = BreakOverlayColor;

        await _settingsRepository.SaveAsync(settings).ConfigureAwait(true);

        _originalLanguage = SelectedLanguage;
        SettingsApplied?.Invoke(this, new SettingsAppliedEventArgs(settings));

        if (closeAfterwards)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ApplyAutoStartAsync()
    {
        try
        {
            await _autoStartCoordinator.SetEnabledAsync(AutoStart).ConfigureAwait(true);
        }
        catch (AutoStartException ex)
        {
            // Autostart ist optional (z. B. durch Gruppenrichtlinie gesperrt):
            // die übrigen Einstellungen werden trotzdem gespeichert.
            SettingsViewModelLog.AutoStartApplyFailed(_logger, ex);
        }
    }

    private void Validate()
    {
        if (!UseCustomDurations)
        {
            WorkError = null;
            BreakError = null;
            return;
        }

        WorkError = WorkMinutes < WorkMinMinutes || WorkMinutes > WorkMaxMinutes
            ? string.Format(CultureInfo.CurrentCulture, _localization.GetText(SettingsTextKeys.ValidationWorkRange), WorkMinMinutes, WorkMaxMinutes)
            : null;

        BreakError = BreakMinutes < BreakMinMinutes || BreakMinutes > BreakMaxMinutes
            ? string.Format(CultureInfo.CurrentCulture, _localization.GetText(SettingsTextKeys.ValidationBreakRange), BreakMinMinutes, BreakMaxMinutes)
            : null;
    }

    private void UpdateModelRanges(BreakModel model)
    {
        BreakInterval defaults = BreakModelRegistry.GetDefault(model);
        WorkDurationRange? range = BreakModelRegistry.GetWorkDurationRange(model);

        HasWorkRange = range.HasValue;
        WorkMinMinutes = range.HasValue
            ? (int)range.Value.Min.TotalMinutes
            : (int)BreakInterval.MinWorkDuration.TotalMinutes;
        WorkMaxMinutes = range.HasValue
            ? (int)range.Value.Max.TotalMinutes
            : (int)(defaults.MaxLimit ?? BreakInterval.MaxWorkDuration).TotalMinutes;

        OnPropertyChanged(nameof(WorkMinMinutes));
        OnPropertyChanged(nameof(WorkMaxMinutes));
        OnPropertyChanged(nameof(HasWorkRange));
        OnPropertyChanged(nameof(WorkRangeText));
    }

    private void OnBreakModelSelected(object? sender, BreakModel model) => SelectModel(model);

    partial void OnSelectedModelOptionChanged(SettingsOption<BreakModel>? value)
    {
        if (_isInitializing || value is null)
        {
            return;
        }

        BreakModels.SetActiveModel(value.Value);
        UpdateModelRanges(value.Value);

        // Bei Modellwechsel die Dauern auf die Modell-Vorgaben zurücksetzen,
        // damit zuvor eingegebene Werte nicht außerhalb des neuen Bereichs liegen.
        BreakInterval defaults = BreakModelRegistry.GetDefault(value.Value);
        WorkMinutes = (int)defaults.WorkDuration.TotalMinutes;
        BreakMinutes = (int)defaults.BreakDuration.TotalMinutes;

        Validate();
    }

    partial void OnSelectedLanguageOptionChanged(SettingsOption<Language>? value)
    {
        if (_isInitializing || value is null)
        {
            return;
        }

        _localization.SetLanguage(value.Value);
    }

    partial void OnUseCustomDurationsChanged(bool value) => Validate();

    partial void OnWorkMinutesChanged(int value) => Validate();

    partial void OnBreakMinutesChanged(int value) => Validate();

    partial void OnWorkErrorChanged(string? value) => NotifyPersistCommands();

    partial void OnBreakErrorChanged(string? value) => NotifyPersistCommands();

    partial void OnBreakOverlayColorChanged(string value) => NotifyPersistCommands();

    private void NotifyPersistCommands()
    {
        OnPropertyChanged(nameof(CanPersist));
        SaveCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (SettingsOption<Language> option in Languages)
        {
            option.RefreshLabel();
        }

        foreach (SettingsOption<BreakModel> option in Models)
        {
            option.RefreshLabel();
        }

        foreach (SettingsOption<SoundType> option in Sounds)
        {
            option.RefreshLabel();
        }

        foreach (SettingsOption<UpdateCheckFrequency> option in UpdateFrequencies)
        {
            option.RefreshLabel();
        }

        foreach (SettingsOption<AppTheme> option in Appearances)
        {
            option.RefreshLabel();
        }

        // Fehlertexte in neuer Sprache, danach alle gebundenen Texte aktualisieren.
        Validate();
        Statistics.RefreshTexts();
        Log.RefreshTexts();

        // Die Modell-Kacheln tragen übersetzte Namen und Sätze; sie werden neu
        // aufgebaut, statt nur die Bindungen anzustoßen. Die Aufgaben ebenso.
        _ = BreakModels.RefreshTextsAsync();
        _ = Tasks.RefreshTextsAsync();

        OnPropertyChanged(string.Empty);
    }

    private List<SettingsOption<Language>> BuildLanguageOptions()
        => Enum.GetValues<Language>()
            .Select(language => new SettingsOption<Language>(
                language,
                () => _localization.GetText(SettingsTextKeys.ForLanguage(language))))
            .ToList();

    private List<SettingsOption<BreakModel>> BuildModelOptions()
        => Enum.GetValues<BreakModel>()
            .Select(model => new SettingsOption<BreakModel>(
                model,
                () => _localization.GetText(SettingsTextKeys.ForModel(model))))
            .ToList();

    private List<SettingsOption<SoundType>> BuildSoundOptions()
        => Enum.GetValues<SoundType>()
            .Select(sound => new SettingsOption<SoundType>(
                sound,
                () => _localization.GetText(SettingsTextKeys.ForSound(sound))))
            .ToList();

    private List<SettingsOption<AppTheme>> BuildThemeOptions()
        => Enum.GetValues<AppTheme>()
            .Select(theme => new SettingsOption<AppTheme>(
                theme,
                () => _localization.GetText(SettingsTextKeys.ForTheme(theme))))
            .ToList();

    /// <summary>Meldet den Sprachwechsel-Handler ab.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        EndCaptureOnDispose();
        BreakModels.ModelSelected -= OnBreakModelSelected;
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Settings-ViewModels.
/// </summary>
internal static partial class SettingsViewModelLog
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Warning,
        Message = "Autostart konnte nicht angewendet werden — übrige Einstellungen wurden trotzdem gespeichert.")]
    public static partial void AutoStartApplyFailed(ILogger logger, Exception exception);
}
