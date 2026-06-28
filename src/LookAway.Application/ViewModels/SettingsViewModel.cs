using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Application.Localization;
using LookAway.Application.Services;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Zustand und Aktionslogik des Settings-Fensters — UI-frei und damit ohne WinUI
/// testbar. Kapselt Laden, Validieren und Persistieren der Benutzerkonfiguration
/// und haelt den Autostart-Eintrag (ueber den <see cref="AutoStartCoordinator"/>)
/// synchron.
/// </summary>
/// <remarks>
/// Sprachwechsel wirken sofort: das Setzen der Auswahl schaltet die
/// <see cref="ILocalizationService"/>-Sprache um, woraufhin alle gebundenen Texte
/// aktualisiert werden. "Abbrechen" stellt die zuletzt gespeicherte Sprache wieder her.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly AutoStartCoordinator _autoStartCoordinator;
    private readonly ILocalizationService _localization;
    private readonly ISoundService _soundService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly string _applicationVersion;

    /// <summary>Statistik-Bereich (BRIEF018), als eigenes ViewModel komponiert.</summary>
    public StatisticsViewModel Statistics { get; }

    private Language _originalLanguage;
    private bool _isInitializing;
    private bool _disposed;

    [ObservableProperty]
    private SettingsOption<Language>? _selectedLanguageOption;

    [ObservableProperty]
    private SettingsOption<BreakModel>? _selectedModelOption;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _useCustomDurations;

    [ObservableProperty]
    private int _workMinutes;

    [ObservableProperty]
    private int _breakMinutes;

    [ObservableProperty]
    private bool _pauseOnIdle;

    [ObservableProperty]
    private int _idleThresholdMinutes;

    [ObservableProperty]
    private bool _suppressOnFullscreen;

    [ObservableProperty]
    private string? _workError;

    [ObservableProperty]
    private string? _breakError;

    [ObservableProperty]
    private bool _soundEnabled;

    [ObservableProperty]
    private SettingsOption<SoundType>? _selectedSoundOption;

    [ObservableProperty]
    private int _soundVolume;

    [ObservableProperty]
    private bool _hotkeysEnabled;

    private HotkeyDefinition _hotkeyStartBreak = HotkeyDefaults.StartBreak;
    private HotkeyDefinition _hotkeySkipOrSnooze = HotkeyDefaults.SkipOrSnooze;
    private HotkeyDefinition _hotkeyToggleDnd = HotkeyDefaults.ToggleDnd;

    /// <summary>
    /// Erzeugt das ViewModel mit seinen Abhaengigkeiten.
    /// </summary>
    /// <param name="settingsRepository">Persistenz der Einstellungen.</param>
    /// <param name="autoStartCoordinator">Haelt Einstellung und Registry synchron.</param>
    /// <param name="localization">Liefert Texte und steuert den Sprachwechsel.</param>
    /// <param name="soundService">Spielt den Erinnerungston fuer die Vorschau.</param>
    /// <param name="statistics">Statistik-ViewModel (komponiert).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="applicationVersion">Anzuzeigende Versionsnummer (Ueber-Bereich).</param>
    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        AutoStartCoordinator autoStartCoordinator,
        ILocalizationService localization,
        ISoundService soundService,
        StatisticsViewModel statistics,
        ILogger<SettingsViewModel> logger,
        string applicationVersion)
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(autoStartCoordinator);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(soundService);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);

        _settingsRepository = settingsRepository;
        _autoStartCoordinator = autoStartCoordinator;
        _localization = localization;
        _soundService = soundService;
        Statistics = statistics;
        _logger = logger;
        _applicationVersion = applicationVersion;

        Languages = BuildLanguageOptions();
        Models = BuildModelOptions();
        Sounds = BuildSoundOptions();

        _localization.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Wird ausgeloest, wenn das Fenster geschlossen werden soll.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Wird nach erfolgreichem Speichern/Anwenden mit den gueltigen Einstellungen
    /// ausgeloest, damit laufende Dienste (Timer, Idle-/Vollbild-Erkennung) sie
    /// sofort uebernehmen koennen.
    /// </summary>
    public event EventHandler<SettingsAppliedEventArgs>? SettingsApplied;

    /// <summary>Auswaehlbare Sprachen mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<Language>> Languages { get; }

    /// <summary>Auswaehlbare Pausenmodelle mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<BreakModel>> Models { get; }

    /// <summary>Auswaehlbare Erinnerungstoene mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<SoundType>> Sounds { get; }

    /// <summary>Untere Grenze der Arbeitsdauer (Minuten) fuer das aktive Modell.</summary>
    public int WorkMinMinutes { get; private set; } = (int)BreakInterval.MinWorkDuration.TotalMinutes;

    /// <summary>Obere Grenze der Arbeitsdauer (Minuten) fuer das aktive Modell.</summary>
    public int WorkMaxMinutes { get; private set; } = (int)BreakInterval.MaxWorkDuration.TotalMinutes;

    /// <summary>Untere Grenze der Pausendauer (Minuten).</summary>
    public int BreakMinMinutes => (int)BreakInterval.MinBreakDuration.TotalMinutes;

    /// <summary>Obere Grenze der Pausendauer (Minuten).</summary>
    public int BreakMaxMinutes => (int)BreakInterval.MaxBreakDuration.TotalMinutes;

    /// <summary>Untere Grenze der Inaktivitaetsschwelle (Minuten).</summary>
    public int IdleMinMinutes => Settings.MinIdleThresholdMinutes;

    /// <summary>Obere Grenze der Inaktivitaetsschwelle (Minuten).</summary>
    public int IdleMaxMinutes => Settings.MaxIdleThresholdMinutes;

    /// <summary>Wahr, wenn das aktive Modell die Arbeitsdauer nur in einem Bereich erlaubt.</summary>
    public bool HasWorkRange { get; private set; }

    /// <summary>Das aktuell gewaehlte Pausenmodell.</summary>
    public BreakModel SelectedModel => SelectedModelOption?.Value ?? BreakModel.ClassicPomodoro;

    /// <summary>Die aktuell gewaehlte Sprache.</summary>
    public Language SelectedLanguage => SelectedLanguageOption?.Value ?? Language.German;

    /// <summary>Der aktuell gewaehlte Erinnerungston.</summary>
    public SoundType SelectedSound => SelectedSoundOption?.Value ?? SoundType.Chime;

    /// <summary>Untere Grenze der Lautstaerke.</summary>
    public int SoundVolumeMin => Settings.MinSoundVolumePercent;

    /// <summary>Obere Grenze der Lautstaerke.</summary>
    public int SoundVolumeMax => Settings.MaxSoundVolumePercent;

    /// <summary>Wahr, wenn die aktuellen Eingaben gespeichert werden duerfen.</summary>
    public bool CanPersist => WorkError is null && BreakError is null;

    /// <summary>Fenstertitel.</summary>
    public string Title => _localization.GetText(SettingsTextKeys.Title);

    /// <summary>Tab-Ueberschrift "Allgemein".</summary>
    public string TabGeneralHeader => _localization.GetText(SettingsTextKeys.TabGeneral);

    /// <summary>Tab-Ueberschrift "Pausenmodell".</summary>
    public string TabModelHeader => _localization.GetText(SettingsTextKeys.TabModel);

    /// <summary>Tab-Ueberschrift "Eigene Intervalle".</summary>
    public string TabIntervalsHeader => _localization.GetText(SettingsTextKeys.TabIntervals);

    /// <summary>Tab-Ueberschrift "Ueber LookAway".</summary>
    public string TabAboutHeader => _localization.GetText(SettingsTextKeys.TabAbout);

    /// <summary>Beschriftung der Sprachauswahl.</summary>
    public string LanguageLabel => _localization.GetText(SettingsTextKeys.LanguageLabel);

    /// <summary>Beschriftung der Autostart-Option.</summary>
    public string AutoStartLabel => _localization.GetText(SettingsTextKeys.AutoStartLabel);

    /// <summary>Beschriftung der Auto-Pause-Option.</summary>
    public string IdlePauseLabel => _localization.GetText(SettingsTextKeys.IdlePauseLabel);

    /// <summary>Beschriftung der Inaktivitaetsschwelle.</summary>
    public string IdleThresholdLabel => _localization.GetText(SettingsTextKeys.IdleThresholdLabel);

    /// <summary>Beschriftung der DND-Option.</summary>
    public string FullscreenSuppressLabel => _localization.GetText(SettingsTextKeys.FullscreenSuppressLabel);

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

    /// <summary>Hinweis auf den erlaubten Arbeitsdauer-Bereich (leer, wenn unbeschraenkt).</summary>
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

    /// <summary>URL zur Dokumentation (fuer einen HyperlinkButton).</summary>
    public Uri DocsUri => new(_localization.GetText(SettingsTextKeys.DocsUrl));

    /// <summary>Beschriftung des Speichern-Buttons.</summary>
    public string SaveLabel => _localization.GetText(SettingsTextKeys.ButtonSave);

    /// <summary>Beschriftung des Abbrechen-Buttons.</summary>
    public string CancelLabel => _localization.GetText(SettingsTextKeys.ButtonCancel);

    /// <summary>Beschriftung des Anwenden-Buttons.</summary>
    public string ApplyLabel => _localization.GetText(SettingsTextKeys.ButtonApply);

    /// <summary>Tab-Ueberschrift "Sound".</summary>
    public string TabSoundHeader => _localization.GetText(SettingsTextKeys.TabSound);

    /// <summary>Beschriftung der Ton-aktivieren-Option.</summary>
    public string SoundEnableLabel => _localization.GetText(SettingsTextKeys.SoundEnableLabel);

    /// <summary>Beschriftung der Ton-Auswahl.</summary>
    public string SoundSelectLabel => _localization.GetText(SettingsTextKeys.SoundSelectLabel);

    /// <summary>Beschriftung der Lautstaerke.</summary>
    public string SoundVolumeLabel => _localization.GetText(SettingsTextKeys.SoundVolumeLabel);

    /// <summary>Beschriftung des Vorhoer-Buttons.</summary>
    public string SoundPreviewLabel => _localization.GetText(SettingsTextKeys.SoundPreviewButton);

    /// <summary>Tab-Ueberschrift "Hotkeys".</summary>
    public string TabHotkeysHeader => _localization.GetText(SettingsTextKeys.TabHotkeys);

    /// <summary>Beschriftung der Hotkey-aktivieren-Option.</summary>
    public string HotkeysEnableLabel => _localization.GetText(SettingsTextKeys.HotkeysEnableLabel);

    /// <summary>Beschriftung der Aktion "Pause starten".</summary>
    public string HotkeyStartBreakLabel => _localization.GetText(SettingsTextKeys.HotkeyStartBreak);

    /// <summary>Beschriftung der Aktion "Ueberspringen/Snooze".</summary>
    public string HotkeySkipOrSnoozeLabel => _localization.GetText(SettingsTextKeys.HotkeySkipOrSnooze);

    /// <summary>Beschriftung der Aktion "DND umschalten".</summary>
    public string HotkeyToggleDndLabel => _localization.GetText(SettingsTextKeys.HotkeyToggleDnd);

    /// <summary>Beschriftung des Zuruecksetzen-Buttons.</summary>
    public string HotkeysResetLabel => _localization.GetText(SettingsTextKeys.HotkeysReset);

    /// <summary>Anzeigetext des "Pause starten"-Hotkeys.</summary>
    public string HotkeyStartBreakText => _hotkeyStartBreak.ToString();

    /// <summary>Anzeigetext des "Ueberspringen/Snooze"-Hotkeys.</summary>
    public string HotkeySkipOrSnoozeText => _hotkeySkipOrSnooze.ToString();

    /// <summary>Anzeigetext des "DND umschalten"-Hotkeys.</summary>
    public string HotkeyToggleDndText => _hotkeyToggleDnd.ToString();

    /// <summary>
    /// Laedt die persistierten Einstellungen in das ViewModel. Vor dem ersten
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
            UpdateModelRanges(settings.BreakModel);

            AutoStart = settings.AutoStart;
            PauseOnIdle = settings.PauseOnIdle;
            IdleThresholdMinutes = settings.IdleThresholdMinutes;
            SuppressOnFullscreen = settings.SuppressOnFullscreen;

            SoundEnabled = settings.SoundEnabled;
            SelectSound(settings.ReminderSound);
            SoundVolume = settings.SoundVolumePercent;

            HotkeysEnabled = settings.HotkeysEnabled;
            _hotkeyStartBreak = settings.HotkeyStartBreak;
            _hotkeySkipOrSnooze = settings.HotkeySkipOrSnooze;
            _hotkeyToggleDnd = settings.HotkeyToggleDnd;

            LoadDurations(settings);
        }
        finally
        {
            _isInitializing = false;
        }

        await Statistics.LoadAsync(cancellationToken).ConfigureAwait(true);

        Validate();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Waehlt das Modell anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="model">Zu waehlendes Pausenmodell.</param>
    public void SelectModel(BreakModel model)
        => SelectedModelOption = Models.First(option => option.Value == model);

    /// <summary>Waehlt die Sprache anhand ihres Werts (UI/Test-Hilfe).</summary>
    /// <param name="language">Zu waehlende Sprache.</param>
    public void SelectLanguage(Language language)
        => SelectedLanguageOption = Languages.First(option => option.Value == language);

    /// <summary>Waehlt den Erinnerungston anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="soundType">Zu waehlender Ton.</param>
    public void SelectSound(SoundType soundType)
        => SelectedSoundOption = Sounds.First(option => option.Value == soundType);

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
    private void ResetHotkeys()
    {
        _hotkeyStartBreak = HotkeyDefaults.StartBreak;
        _hotkeySkipOrSnooze = HotkeyDefaults.SkipOrSnooze;
        _hotkeyToggleDnd = HotkeyDefaults.ToggleDnd;
        OnPropertyChanged(nameof(HotkeyStartBreakText));
        OnPropertyChanged(nameof(HotkeySkipOrSnoozeText));
        OnPropertyChanged(nameof(HotkeyToggleDndText));
    }

    [RelayCommand]
    private void Cancel()
    {
        // Nur Vorschau-Aenderungen verwerfen: zuletzt gespeicherte Sprache zurueck.
        _localization.SetLanguage(_originalLanguage);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task PersistAsync(bool closeAfterwards)
    {
        await ApplyAutoStartAsync().ConfigureAwait(true);

        Settings settings = await _settingsRepository.LoadAsync().ConfigureAwait(true);
        settings.Language = SelectedLanguage;
        settings.BreakModel = SelectedModel;
        settings.PauseOnIdle = PauseOnIdle;
        settings.IdleThresholdMinutes = Math.Clamp(IdleThresholdMinutes, IdleMinMinutes, IdleMaxMinutes);
        settings.SuppressOnFullscreen = SuppressOnFullscreen;
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
            // die uebrigen Einstellungen werden trotzdem gespeichert.
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

    partial void OnSelectedModelOptionChanged(SettingsOption<BreakModel>? value)
    {
        if (_isInitializing || value is null)
        {
            return;
        }

        UpdateModelRanges(value.Value);

        // Bei Modellwechsel die Dauern auf die Modell-Vorgaben zuruecksetzen,
        // damit zuvor eingegebene Werte nicht ausserhalb des neuen Bereichs liegen.
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

        // Fehlertexte in neuer Sprache, danach alle gebundenen Texte aktualisieren.
        Validate();
        Statistics.RefreshTexts();
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

    /// <summary>Meldet den Sprachwechsel-Handler ab.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
        Message = "Autostart konnte nicht angewendet werden — uebrige Einstellungen wurden trotzdem gespeichert.")]
    public static partial void AutoStartApplyFailed(ILogger logger, Exception exception);
}
