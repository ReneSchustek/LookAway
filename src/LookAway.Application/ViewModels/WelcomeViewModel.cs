using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Application.Localization;
using LookAway.Application.Services;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Zustand und Ablauflogik des First-Run-Wizards — UI-frei und damit
/// ohne WinUI testbar. Fuehrt in drei Schritten durch Sprache, Pausenmodell und
/// Autostart und persistiert beim Abschluss die Erstkonfiguration.
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject, IDisposable
{
    /// <summary>Anzahl der Wizard-Schritte.</summary>
    public const int TotalSteps = 3;

    /// <summary>Default-Pausenmodell der Erstkonfiguration.</summary>
    public const BreakModel DefaultModel = BreakModel.ModifiedPomodoro;

    private readonly ISettingsRepository _settingsRepository;
    private readonly AutoStartCoordinator _autoStartCoordinator;
    private readonly ILocalizationService _localization;
    private readonly ILogger<WelcomeViewModel> _logger;

    private bool _disposed;

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private SettingsOption<Language>? _selectedLanguageOption;

    [ObservableProperty]
    private SettingsOption<BreakModel>? _selectedModelOption;

    [ObservableProperty]
    private bool _autoStart = true;

    /// <summary>
    /// Erzeugt das ViewModel und waehlt die erkannte Sprache vor.
    /// </summary>
    /// <param name="settingsRepository">Persistenz der Einstellungen.</param>
    /// <param name="autoStartCoordinator">Haelt Einstellung und Registry synchron.</param>
    /// <param name="localization">Liefert Texte und steuert den Sprachwechsel.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="detectedLanguage">Anhand der Systemkultur erkannte Startsprache.</param>
    public WelcomeViewModel(
        ISettingsRepository settingsRepository,
        AutoStartCoordinator autoStartCoordinator,
        ILocalizationService localization,
        ILogger<WelcomeViewModel> logger,
        Language detectedLanguage)
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(autoStartCoordinator);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsRepository = settingsRepository;
        _autoStartCoordinator = autoStartCoordinator;
        _localization = localization;
        _logger = logger;

        Languages = Enum.GetValues<Language>()
            .Select(language => new SettingsOption<Language>(
                language,
                () => _localization.GetText(SettingsTextKeys.ForLanguage(language))))
            .ToList();
        Models = Enum.GetValues<BreakModel>()
            .Select(model => new SettingsOption<BreakModel>(
                model,
                () => _localization.GetText(SettingsTextKeys.ForModel(model))))
            .ToList();

        _localization.LanguageChanged += OnLanguageChanged;
        _localization.SetLanguage(detectedLanguage);

        SelectedLanguageOption = Languages.First(option => option.Value == detectedLanguage);
        SelectedModelOption = Models.First(option => option.Value == DefaultModel);
    }

    /// <summary>Wird ausgeloest, wenn der Wizard erfolgreich abgeschlossen wurde.</summary>
    public event EventHandler<SettingsAppliedEventArgs>? Completed;

    /// <summary>Auswaehlbare Sprachen mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<Language>> Languages { get; }

    /// <summary>Auswaehlbare Pausenmodelle mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<BreakModel>> Models { get; }

    /// <summary>Die aktuell gewaehlte Sprache.</summary>
    public Language SelectedLanguage => SelectedLanguageOption?.Value ?? Language.German;

    /// <summary>Das aktuell gewaehlte Pausenmodell.</summary>
    public BreakModel SelectedModel => SelectedModelOption?.Value ?? DefaultModel;

    /// <summary>Wahr, solange der erste Schritt aktiv ist.</summary>
    public bool IsFirstStep => CurrentStep == 0;

    /// <summary>Wahr, sobald der letzte Schritt aktiv ist.</summary>
    public bool IsLastStep => CurrentStep == TotalSteps - 1;

    /// <summary>Wahr, wenn der Sprachschritt aktiv ist.</summary>
    public bool IsStepLanguage => CurrentStep == 0;

    /// <summary>Wahr, wenn der Modellschritt aktiv ist.</summary>
    public bool IsStepModel => CurrentStep == 1;

    /// <summary>Wahr, wenn der Autostart-Schritt aktiv ist.</summary>
    public bool IsStepAutoStart => CurrentStep == 2;

    /// <summary>Soll der Zurueck-Button sichtbar sein?</summary>
    public bool ShowBackButton => !IsFirstStep;

    /// <summary>Soll der Weiter-Button sichtbar sein?</summary>
    public bool ShowNextButton => !IsLastStep;

    /// <summary>Soll der Fertig-Button sichtbar sein?</summary>
    public bool ShowFinishButton => IsLastStep;

    /// <summary>Begruessung.</summary>
    public string Title => _localization.GetText(WelcomeTextKeys.Title);

    /// <summary>Einleitungstext.</summary>
    public string Intro => _localization.GetText(WelcomeTextKeys.Intro);

    /// <summary>Ueberschrift des aktuellen Schritts.</summary>
    public string StepHeader => _localization.GetText(CurrentStep switch
    {
        0 => WelcomeTextKeys.StepLanguage,
        1 => WelcomeTextKeys.StepModel,
        _ => WelcomeTextKeys.StepAutoStart,
    });

    /// <summary>Fortschrittsanzeige "Schritt X von Y".</summary>
    public string StepProgress => string.Format(
        CultureInfo.CurrentCulture,
        _localization.GetText(WelcomeTextKeys.StepProgress),
        CurrentStep + 1,
        TotalSteps);

    /// <summary>Beschriftung der Sprachauswahl.</summary>
    public string LanguageLabel => _localization.GetText(SettingsTextKeys.LanguageLabel);

    /// <summary>Beschriftung der Modellauswahl.</summary>
    public string ModelLabel => _localization.GetText(SettingsTextKeys.ModelLabel);

    /// <summary>Beschriftung der Autostart-Option.</summary>
    public string AutoStartLabel => _localization.GetText(SettingsTextKeys.AutoStartLabel);

    /// <summary>Hinweis zum Autostart.</summary>
    public string AutoStartHint => _localization.GetText(WelcomeTextKeys.AutoStartHint);

    /// <summary>Hinweis zum Schliessen ohne Abschluss.</summary>
    public string CloseHint => _localization.GetText(WelcomeTextKeys.CloseHint);

    /// <summary>Beschriftung des Zurueck-Buttons.</summary>
    public string BackLabel => _localization.GetText(WelcomeTextKeys.ButtonBack);

    /// <summary>Beschriftung des Weiter-Buttons.</summary>
    public string NextLabel => _localization.GetText(WelcomeTextKeys.ButtonNext);

    /// <summary>Beschriftung des Fertig-Buttons.</summary>
    public string FinishLabel => _localization.GetText(WelcomeTextKeys.ButtonFinish);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (!IsLastStep)
        {
            CurrentStep++;
        }
    }

    private bool CanGoNext() => !IsLastStep;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (!IsFirstStep)
        {
            CurrentStep--;
        }
    }

    private bool CanGoBack() => !IsFirstStep;

    [RelayCommand(CanExecute = nameof(IsLastStep))]
    private async Task FinishAsync()
    {
        await ApplyAutoStartAsync().ConfigureAwait(true);

        Settings settings = await _settingsRepository.LoadAsync().ConfigureAwait(true);
        settings.Language = SelectedLanguage;
        settings.BreakModel = SelectedModel;
        await _settingsRepository.SaveAsync(settings).ConfigureAwait(true);

        Completed?.Invoke(this, new SettingsAppliedEventArgs(settings));
    }

    private async Task ApplyAutoStartAsync()
    {
        try
        {
            await _autoStartCoordinator.SetEnabledAsync(AutoStart).ConfigureAwait(true);
        }
        catch (AutoStartException ex)
        {
            // Autostart ist optional — die Erstkonfiguration wird trotzdem gespeichert.
            WelcomeViewModelLog.AutoStartApplyFailed(_logger, ex);
        }
    }

    partial void OnCurrentStepChanged(int value)
    {
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(IsStepLanguage));
        OnPropertyChanged(nameof(IsStepModel));
        OnPropertyChanged(nameof(IsStepAutoStart));
        OnPropertyChanged(nameof(ShowBackButton));
        OnPropertyChanged(nameof(ShowNextButton));
        OnPropertyChanged(nameof(ShowFinishButton));
        OnPropertyChanged(nameof(StepHeader));
        OnPropertyChanged(nameof(StepProgress));
    }

    partial void OnSelectedLanguageOptionChanged(SettingsOption<Language>? value)
    {
        if (value is not null)
        {
            _localization.SetLanguage(value.Value);
        }
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

        OnPropertyChanged(string.Empty);
    }

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
/// Source-generierte Logging-Methoden des Welcome-ViewModels.
/// </summary>
internal static partial class WelcomeViewModelLog
{
    [LoggerMessage(
        EventId = 1210,
        Level = LogLevel.Warning,
        Message = "Autostart konnte beim Erststart nicht gesetzt werden — Erstkonfiguration wurde trotzdem gespeichert.")]
    public static partial void AutoStartApplyFailed(ILogger logger, Exception exception);
}
