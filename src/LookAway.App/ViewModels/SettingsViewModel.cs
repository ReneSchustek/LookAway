using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.App.Services;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;
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
/// Die Seiten stehen in Teilen der Klasse: Allgemein hier, Intervalle, Ton und
/// Pausen-Aktionen, Hotkeys und Updates in eigenen Dateien.
/// </remarks>
internal sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly AutoStartCoordinator _autoStartCoordinator;
    private readonly ILocalizationService _localization;
    private readonly ISoundService _soundService;
    private readonly IUpdateChecker _updateChecker;
    private readonly IUpdateInstaller _updateInstaller;
    private readonly ISetupUpdate _setupUpdate;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly string _applicationVersion;

    private Language _originalLanguage;
    private bool _isInitializing;
    private bool _disposed;

    /// <summary>
    /// Erzeugt das ViewModel mit seinen Abhängigkeiten.
    /// </summary>
    /// <param name="services">Dienste für Laden, Speichern, Autostart, Ton und Updates.</param>
    /// <param name="sections">Die eigenständigen Bereiche des Fensters (komponiert).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="applicationVersion">Anzuzeigende Versionsnummer (Über-Bereich).</param>
    public SettingsViewModel(
        SettingsServices services,
        SettingsSections sections,
        ILogger<SettingsViewModel> logger,
        string applicationVersion)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);

        (_settingsRepository, _autoStartCoordinator, _localization, _soundService, _updateChecker, _updateInstaller, _setupUpdate) = services;
        (Statistics, BreakModels, Log, Tasks) = sections;
        Texts = new SettingsTexts(_localization);
        _logger = logger;
        _applicationVersion = applicationVersion;

        // Die Kachel-Auswahl und das Auswahlfeld führen dieselbe Einstellung. Damit
        // es dabei nur eine Wahrheit gibt, meldet die Liste ihre Wahl hierher und
        // bekommt umgekehrt die Marke gesetzt, wenn sie anderswo umgestellt wird.
        BreakModels.ModelSelected += OnBreakModelSelected;

        InitializeBindings();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    // Startwerte der gebundenen Eigenschaften und die Auswahllisten; partielle Properties
    // erlauben keine Feld-Initialisierer.
    private void InitializeBindings()
    {
        BreakOverlayColor = Settings.DefaultBreakOverlayColor;
        BreakOverlayImageDimPercent = Settings.DefaultOverlayImageDimPercent;
        UpdateStatusText = string.Empty;
        BuildAllOptions();
    }

    /// <summary>Wird ausgelöst, wenn das Fenster geschlossen werden soll.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Wird nach erfolgreichem Speichern/Anwenden mit den gültigen Einstellungen
    /// ausgelöst, damit laufende Dienste (Timer, Idle-/Vollbild-Erkennung) sie
    /// sofort übernehmen können.
    /// </summary>
    public event EventHandler<SettingsAppliedEventArgs>? SettingsApplied;

    /// <summary>
    /// Die Beschriftungen des Fensters, als eigenes Objekt komponiert.
    /// </summary>
    /// <remarks>
    /// Beschriftungen nachzuschlagen ist eine andere Aufgabe als Einstellungen zu
    /// laden, zu prüfen und zu speichern — siehe <see cref="SettingsTexts"/>.
    /// </remarks>
    public SettingsTexts Texts { get; }

    /// <summary>Statistik-Bereich, als eigenes ViewModel komponiert.</summary>
    public StatisticsViewModel Statistics { get; }

    /// <summary>Pausenmodell-Liste mit Suche und Filter, als eigenes ViewModel komponiert.</summary>
    public BreakModelListViewModel BreakModels { get; }

    /// <summary>Protokoll-Ansicht mit Suche und Filter, als eigenes ViewModel komponiert.</summary>
    public LogViewModel Log { get; }

    /// <summary>Aufgabenliste mit Suche und Filter, als eigenes ViewModel komponiert.</summary>
    public WorkTaskListViewModel Tasks { get; }

    [ObservableProperty]
    public partial SettingsOption<Language>? SelectedLanguageOption { get; set; }

    [ObservableProperty]
    public partial SettingsOption<BreakModel>? SelectedModelOption { get; set; }

    [ObservableProperty]
    public partial SettingsOption<AppTheme>? SelectedThemeOption { get; set; }

    [ObservableProperty]
    public partial bool AutoStart { get; set; }

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

    /// <summary>Das aktuell gewählte Pausenmodell.</summary>
    public BreakModel SelectedModel => SelectedModelOption?.Value ?? Settings.DefaultBreakModel;

    /// <summary>Die aktuell gewählte Sprache.</summary>
    public Language SelectedLanguage => SelectedLanguageOption?.Value ?? Language.German;

    /// <summary>Das aktuell gewählte Erscheinungsbild.</summary>
    public AppTheme SelectedTheme => SelectedThemeOption?.Value ?? AppTheme.System;

    /// <summary>Wahr, wenn die aktuellen Eingaben gespeichert werden dürfen.</summary>
    public bool CanPersist => WorkError is null && BreakError is null && HexColor.IsValid(BreakOverlayColor);

    /// <summary>
    /// Lädt die persistierten Einstellungen in das ViewModel. Vor dem ersten
    /// Anzeigen aufzurufen.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Der Ladevorgang samt der eigenständigen Bereiche.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _isInitializing = true;
        try
        {
            Settings settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(true);
            LoadGeneral(settings);
            LoadBreakSettings(settings);
            LoadHotkeys(settings);
            LoadUpdates(settings);
            LoadDurations(settings);
        }
        finally
        {
            _isInitializing = false;
        }

        await LoadSectionsAsync(cancellationToken).ConfigureAwait(true);
        Validate();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Wählt das Modell anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="model">Zu wählendes Pausenmodell.</param>
    public void SelectModel(BreakModel model)
        => SelectedModelOption = Models.First(option => option.Value == model);

    /// <summary>Wählt das Erscheinungsbild anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="theme">Zu wählendes Erscheinungsbild.</param>
    public void SelectTheme(AppTheme theme)
        => SelectedThemeOption = Appearances.First(option => option.Value == theme);

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

    private void LoadGeneral(Settings settings)
    {
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
    }

    private async Task LoadSectionsAsync(CancellationToken cancellationToken)
    {
        await Statistics.LoadAsync(cancellationToken).ConfigureAwait(true);
        BreakModels.SetActiveModel(SelectedModel);
        await BreakModels.LoadAsync(cancellationToken).ConfigureAwait(true);
        await Log.LoadAsync(cancellationToken).ConfigureAwait(true);
        await Tasks.LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanPersist))]
    private Task SaveAsync() => PersistAsync(closeAfterwards: true);

    [RelayCommand(CanExecute = nameof(CanPersist))]
    private Task ApplyAsync() => PersistAsync(closeAfterwards: false);

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
        StoreGeneral(settings);
        StoreDurations(settings);
        StoreBreakSettings(settings);
        StoreHotkeys(settings);
        StoreUpdates(settings);
        await _settingsRepository.SaveAsync(settings).ConfigureAwait(true);

        _originalLanguage = SelectedLanguage;
        SettingsApplied?.Invoke(this, new SettingsAppliedEventArgs(settings));

        if (closeAfterwards)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StoreGeneral(Settings settings)
    {
        settings.Language = SelectedLanguage;
        settings.BreakModel = SelectedModel;
        settings.AppTheme = SelectedTheme;
        settings.PauseOnIdle = PauseOnIdle;
        settings.IdleThresholdMinutes = Math.Clamp(IdleThresholdMinutes, IdleMinMinutes, IdleMaxMinutes);
        settings.SuppressOnFullscreen = SuppressOnFullscreen;
        settings.AutoStartBreakEnabled = AutoStartBreakEnabled;
        settings.AutoStartBreakSeconds = Math.Clamp(AutoStartBreakSeconds, AutoStartBreakMinSeconds, AutoStartBreakMaxSeconds);
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

    private void OnBreakModelSelected(object? sender, BreakModel model) => SelectModel(model);

    private void NotifyPersistCommands()
    {
        OnPropertyChanged(nameof(CanPersist));
        SaveCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
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

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Warning,
        Message = "Der Dateidialog für das Overlay-Bild ist gescheitert; die Bildwahl bleibt unverändert.")]
    public static partial void OverlayImagePickFailed(ILogger logger, Exception exception);
}
