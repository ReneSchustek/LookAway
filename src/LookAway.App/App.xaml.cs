using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data;
using LookAway.Data.Logging;
using LookAway.Data.Net;
using LookAway.Data.Power;
using LookAway.Data.Repositories;
using LookAway.Data.Services;
using LookAway.Data.Time;
using LookAway.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

// Aliase auflösen Namespace-Kollisionen mit Microsoft.UI.Xaml und System.
using AutoStartCoordinator = LookAway.Core.Services.AutoStartCoordinator;
using SettingsViewModel = LookAway.App.ViewModels.SettingsViewModel;
using StatisticsViewModel = LookAway.App.ViewModels.StatisticsViewModel;
using WelcomeViewModel = LookAway.App.ViewModels.WelcomeViewModel;
using StatisticsService = LookAway.Core.Services.StatisticsService;
using CsvExporter = LookAway.Core.Services.CsvExporter;
using FullscreenDetectionService = LookAway.Core.Services.FullscreenDetectionService;
using IdleDetectionService = LookAway.Core.Services.IdleDetectionService;
using LogService = LookAway.Core.Services.LogService;
using PauseActionService = LookAway.Core.Services.PauseActionService;
using BreakCoordinator = LookAway.Core.Services.BreakCoordinator;
using UpdateInstallerService = LookAway.Data.Update.UpdateInstallerService;
using IUpdateInstaller = LookAway.Core.Interfaces.IUpdateInstaller;
using UpdateApplyArgs = LookAway.Core.ValueObjects.UpdateApplyArgs;
using TrayStatusPresenter = LookAway.Core.Services.TrayStatusPresenter;
using SingleInstanceLock = LookAway.Data.Services.SingleInstanceLock;
using TimerService = LookAway.Core.Services.TimerService;
using XamlUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;
using SystemUnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;

namespace LookAway.App;

/// <summary>
/// Anwendungs-Bootstrap. Konfiguriert das DI-Container, das Logging
/// (Datei-Sink mit Rotation), die globalen Crash-Handler und das
/// Tray-Icon. Stellt sicher, dass nur eine Instanz pro Benutzer läuft.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Der WinUI-3-XAML-Compiler erzeugt die zweite Partialklasse als 'public'; ein abweichender Modifizierer ist nicht kompilierbar.")]
public sealed partial class LookAwayApp : global::Microsoft.UI.Xaml.Application, IDisposable
{
    private const string LogFolderName = "logs";
    private const string CrashSourceAppDomain = "AppDomain.UnhandledException";
    private const string CrashSourceTaskScheduler = "TaskScheduler.UnobservedTaskException";
    private const string CrashSourceWinUi = "Application.UnhandledException";
    private const string ShutdownReasonUserExit = "UserRequested";
    private const string ShutdownReasonSecondInstance = "SecondInstanceDetected";

    // Service-Provider der Composition Root. Bewusst privat: außerhalb dieser Klasse
    // werden Abhängigkeiten per Konstruktor injiziert, nicht nachgeschlagen.
    private static IServiceProvider Services { get; set; } = null!;

    private const int HistoryRetentionDays = 365;

    private Window? _window;
    private LogService? _logService;
    private ILogger<LookAwayApp>? _logger;
    private SingleInstanceLock? _instanceLock;
    private TrayIconService? _trayIcon;
    private DetectionLoopHost? _detectionLoop;
    private ReminderPresenter? _reminderPresenter;
    private BreakOverlayPresenter? _overlayPresenter;
    private SettingsPresenter? _settingsPresenter;

    // Zuletzt angewendete Einstellungen — Grundlage, um die Hotkeys nach einer
    // Aufnahme wieder in genau dem Zustand zu registrieren, der vorher galt.
    private Settings? _currentSettings;
    private BreakCoordinator? _coordinator;
    private UpdateOrchestrator? _updateOrchestrator;

    /// <summary>
    /// Initialisiert die Anwendung, das DI-Container und die globalen Handler.
    /// </summary>
    public LookAwayApp()
    {
        try
        {
            InitializeComponent();

            // Update-Helfer: Er startet aus dem Staging-Ordner, bedient aber die Installation
            // im Zielordner. Sein Datenort muss deshalb der der Installation sein — sonst läse
            // er Einstellungen und Logs neben sich und fände den vermerkten Datei-Hash nicht,
            // mit dem er die Quelle prüft. Muss vor dem Aufbau der Dienste geschehen.
            if (UpdateApplyArgs.TryParse(Environment.GetCommandLineArgs(), out UpdateApplyArgs helperArgs))
            {
                AppDataLocation.UseBaseDirectory(helperArgs.Target);
            }

            Services = ServiceRegistration.Build(
                AppDataLocation.GetDataDirectory(),
                ParseVersion(GetVersion()),
                IsDebugBuild());
            _logService = Services.GetRequiredService<LogService>();
            _logger = Services.GetRequiredService<ILogger<LookAwayApp>>();

            RegisterGlobalCrashHandlers();
            UnhandledException += OnApplicationUnhandledException;

            _updateOrchestrator = CreateUpdateOrchestrator();

            Services.GetRequiredService<IPowerModeWatcher>().Start();

            bool lastRunCrashed = _logService.LogStart(GetVersion(), Language.German);
            if (lastRunCrashed)
            {
                AppLog.LastRunCrashed(_logger);
            }
        }
        catch (Exception ex)
        {
            // Startfehler vor der Handler-Registrierung werden sonst nicht
            // protokolliert. Bestes Bemühen: in eine Datei schreiben, dann weiter.
            WriteStartupError(ex);
            throw;
        }
    }

    private UpdateOrchestrator CreateUpdateOrchestrator()
    {
        UpdateOrchestrator orchestrator = new(
            Services.GetRequiredService<UpdateInstallerService>(),
            Services.GetRequiredService<IUpdateChecker>(),
            Services.GetRequiredService<ISettingsRepository>(),
            Services.GetRequiredService<IClock>(),
            ParseVersion(GetVersion()),
            Services.GetRequiredService<ILogger<UpdateOrchestrator>>())
        {
            UpdateAvailableChanged = available => _trayIcon?.SetUpdateAvailable(available),
            RelaunchRequested = RequestExit,
        };
        return orchestrator;
    }

    private static void WriteStartupError(Exception exception)
    {
        try
        {
            string directory = Path.Combine(AppDataLocation.GetDataDirectory(), LogFolderName);
            _ = Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "startup-error.log");
            File.AppendAllText(path, $"[{DateTimeOffset.UtcNow:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Diagnose ist best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Diagnose ist best-effort.
        }
    }

    /// <summary>
    /// Wird beim Start aufgerufen. Prüfen Single-Instance, sonst Zeit-
    /// instanz signalisieren und beenden. Bei alleiniger Instanz: Tray-Icon
    /// einblenden und das Hauptfenster verborgen halten.
    /// </summary>
    /// <param name="args">Vom System gelieferte Startparameter.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Helfer-Modus: aus dem Staging-Ordner gestartet, um nach dem Beenden der
        // alten Instanz die Programmdateien zu ersetzen und neu zu starten.
        if (UpdateApplyArgs.TryParse(Environment.GetCommandLineArgs(), out UpdateApplyArgs applyArgs))
        {
            _updateOrchestrator!.RunHelperApply(applyArgs);
            return;
        }

        _instanceLock = new SingleInstanceLock(Environment.UserName);
        if (!_instanceLock.TryAcquire())
        {
            AppLog.SecondInstanceDetected(_logger!);
            _ = _instanceLock.SignalExistingInstance();
            _logService?.LogShutdown(ShutdownReasonSecondInstance);
            Exit();
            return;
        }

        // Der Rest des Starts liest die Einstellungen und darf den UI-Thread nicht
        // blockieren; OnLaunched ist ein synchroner Framework-Einstiegspunkt.
        _ = ContinueLaunchAsync();
    }

    private async Task ContinueLaunchAsync()
    {
        // Ausstehendes Update beim Start anwenden (Datei-Tausch via Helfer-Prozess);
        // beendet diese Instanz, falls ein Update eingespielt wird.
        if (await _updateOrchestrator!.TryApplyPendingUpdateOnStartupAsync().ConfigureAwait(true))
        {
            // Lock freigeben und beenden, damit der Helfer die Dateien ersetzen kann.
            _instanceLock?.Dispose();
            _instanceLock = null;
            _logService?.LogShutdown(ShutdownReasonUserExit);
            Exit();
            return;
        }

        _instanceLock!.ActivationRequested += OnActivationRequested;

        _window = new MainWindow();
        // Hauptfenster bleibt verborgen — die App lebt im Tray.
        _window.AppWindow.Hide();

        _reminderPresenter = new ReminderPresenter(
            _window.DispatcherQueue,
            Services.GetRequiredService<ILocalizationService>(),
            Services.GetRequiredService<ILogger<ReminderPresenter>>());
        _overlayPresenter = new BreakOverlayPresenter(
            _window.DispatcherQueue,
            Services.GetRequiredService<ILocalizationService>(),
            Services.GetRequiredService<ILogger<BreakOverlayPresenter>>());
        _settingsPresenter = new SettingsPresenter(
            _window.DispatcherQueue,
            CreateSettingsViewModel,
            ApplySettingsLive,
            SuspendHotkeysWhileCapturing);

        await StartAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Startsequenz: beim allerersten Start führt der Wizard durch die
    /// Erstkonfiguration; danach werden Tray und Timer eingerichtet.
    /// </summary>
    private async Task StartAsync()
    {
        try
        {
            ISettingsRepository repository = Services.GetRequiredService<ISettingsRepository>();
            Settings settings = await repository.LoadAsync().ConfigureAwait(true);

            if (settings.IsFirstRun)
            {
                bool completed = await ShowWelcomeAsync().ConfigureAwait(true);
                if (!completed)
                {
                    // Der Benutzer hat den Wizard ohne Abschluss geschlossen —
                    // ohne gültige Konfiguration wird nicht weitergestartet.
                    _logService?.LogShutdown(ShutdownReasonUserExit);
                    Exit();
                    return;
                }

                settings = await repository.LoadAsync().ConfigureAwait(true);
            }

            // Anzeigesprache aus der Konfiguration übernehmen.
            Services.GetRequiredService<ILocalizationService>().SetLanguage(settings.Language);

            InitializeTray();

            // Registry ist die führende Quelle für den Autostart-Zustand: ein
            // manueller Eingriff wird übernommen, ein veralteter Pfad korrigiert.
            _ = SynchronizeAutoStartAsync();

            // Alte Historie-Einträge (>1 Jahr) aufräumen — nicht startkritisch.
            _ = PurgeHistoryAsync();

            StartTimer(settings);

            // Update-Prüfung im Hintergrund — nicht startkritisch.
            _ = _updateOrchestrator!.CheckAtStartupAsync(settings);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.TimerStartFailed(_logger!, ex);
        }
        catch (IOException ex)
        {
            AppLog.TimerStartFailed(_logger!, ex);
        }
    }

    private static Version ParseVersion(string version)
        => Version.TryParse(version, out Version? parsed) ? parsed : new Version(0, 0, 0);

    private void InitializeTray()
    {
        _trayIcon = new TrayIconService(
            Services.GetRequiredService<ITimerService>(),
            Services.GetRequiredService<TrayStatusPresenter>(),
            Services.GetRequiredService<ILocalizationService>(),
            _window!.DispatcherQueue,
            Services.GetRequiredService<ILogger<TrayIconService>>(),
            OpenSettings,
            RequestExit,
            _updateOrchestrator!.HandleManualUpdateRequested,
            () => _coordinator?.RequestReminder());

        _trayIcon.Show();
        AppLog.TrayReady(_logger!);
    }

    // Timer mit dem konfigurierten Modell starten — das Tray-Icon spiegelt den
    // Zustand dann live wider.
    private void StartTimer(Settings settings)
    {
        ITimerService timerService = Services.GetRequiredService<ITimerService>();
        _coordinator = new BreakCoordinator(
            timerService,
            _reminderPresenter!,
            _overlayPresenter!,
            Services.GetRequiredService<PauseActionService>(),
            Services.GetRequiredService<ISoundService>(),
            Services.GetRequiredService<IClock>(),
            Services.GetRequiredService<IBreakHistoryRepository>(),
            _trayIcon!,
            Services.GetRequiredService<FullscreenDetectionService>(),
            Services.GetRequiredService<ILogger<BreakCoordinator>>());

        // Innerhalb derselben Windows-Sitzung (z. B. nach einer Aktualisierung) den
        // Countdown fortsetzen statt zurückzusetzen; bei einem Windows-Neustart
        // (neue Sitzung) startet er regulär neu.
        _coordinator.ApplySchedule(settings, LoadResumeRemaining(settings));

        _detectionLoop = new DetectionLoopHost(
            Services.GetRequiredService<IdleDetectionService>(),
            Services.GetRequiredService<FullscreenDetectionService>(),
            timerService,
            _coordinator);
        _detectionLoop.ApplySettings(settings);
        _detectionLoop.Start();

        RegisterHotkeys(settings);
    }

    /// <summary>
    /// Liefert die fortzusetzende Restarbeitszeit, wenn eine Momentaufnahme aus
    /// <em>derselben</em> Windows-Sitzung und zum aktuellen Pausenmodell vorliegt;
    /// sonst <c>null</c> (regulärer Start mit voller Arbeitsdauer). Die Momentaufnahme
    /// wird in jedem Fall verbraucht (einmalige Verwendung).
    /// </summary>
    private TimeSpan? LoadResumeRemaining(Settings settings)
    {
        ITimerStateStore store = Services.GetRequiredService<ITimerStateStore>();
        TimerSnapshot? snapshot = store.Load();
        if (snapshot is null)
        {
            return null;
        }

        store.Clear();

        DateTimeOffset currentMarker = SessionMarker.Compute(
            Services.GetRequiredService<IClock>().UtcNow,
            Environment.TickCount64);

        bool sameSession = SessionMarker.IsSameSession(snapshot.SessionMarker, currentMarker);
        return sameSession
            && snapshot.Model == settings.BreakModel
            && snapshot.WorkRemaining > TimeSpan.Zero
            ? snapshot.WorkRemaining
            : null;
    }

    /// <summary>
    /// Sichert beim Beenden den laufenden Arbeits-Countdown samt Sitzungsmarke, damit
    /// ihn ein Neustart in derselben Sitzung fortsetzen kann. Nur während einer
    /// Arbeitsphase sinnvoll; in Pause/Idle wird nichts gesichert.
    /// </summary>
    private void PersistTimerSnapshot()
    {
        if (_coordinator is not { IsWorking: true })
        {
            return;
        }

        DateTimeOffset marker = SessionMarker.Compute(
            Services.GetRequiredService<IClock>().UtcNow,
            Environment.TickCount64);
        Services.GetRequiredService<ITimerStateStore>().Save(
            new TimerSnapshot(_coordinator.ActiveModel, _coordinator.WorkRemaining, marker));
    }

    /// <summary>
    /// Gibt die globalen Hotkeys frei, solange in den Einstellungen eine
    /// Tastenkombination aufgenommen wird, und registriert sie danach wieder.
    /// <para>
    /// Ohne das erreicht die gedrückte Kombination das Fenster nicht: Windows
    /// liefert einen registrierten Hotkey an den Registrierenden aus, nicht an das
    /// fokussierte Fenster. Der Benutzer löste damit die Aktion aus, statt sie neu
    /// zu belegen.
    /// </para>
    /// </summary>
    /// <param name="aufnahmeLaeuft">Wahr bei Beginn, falsch bei Ende der Aufnahme.</param>
    private void SuspendHotkeysWhileCapturing(bool aufnahmeLaeuft)
    {
        if (aufnahmeLaeuft)
        {
            Services.GetRequiredService<IHotkeyService>().UnregisterAll();
            return;
        }

        if (_currentSettings is not null)
        {
            RegisterHotkeys(_currentSettings);
        }
    }

    private void RegisterHotkeys(Settings settings)
    {
        _currentSettings = settings;
        IHotkeyService hotkeys = Services.GetRequiredService<IHotkeyService>();
        hotkeys.HotkeyPressed -= OnHotkeyPressed;
        hotkeys.HotkeyPressed += OnHotkeyPressed;

        if (!settings.HotkeysEnabled)
        {
            hotkeys.UnregisterAll();
            return;
        }

        Dictionary<HotkeyAction, HotkeyDefinition> bindings = new()
        {
            [HotkeyAction.StartBreak] = settings.HotkeyStartBreak,
            [HotkeyAction.SkipOrSnooze] = settings.HotkeySkipOrSnooze,
            [HotkeyAction.ToggleDnd] = settings.HotkeyToggleDnd,
        };
        hotkeys.Register(bindings);
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
        => _ = _window?.DispatcherQueue.TryEnqueue(() => HandleHotkey(e.Action));

    private void HandleHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.StartBreak:
                _coordinator?.RequestReminder();
                break;
            case HotkeyAction.SkipOrSnooze:
                _coordinator?.SkipOrSnooze();
                break;
            case HotkeyAction.ToggleDnd:
                _coordinator?.ToggleManualDnd();
                break;
            default:
                break;
        }
    }

    private Task<bool> ShowWelcomeAsync()
    {
        WelcomePresenter presenter = new(
            _window!.DispatcherQueue,
            CreateWelcomeViewModel,
            _ => { });
        return presenter.ShowAsync();
    }

    private WelcomeViewModel CreateWelcomeViewModel() => new(
        Services.GetRequiredService<ISettingsRepository>(),
        Services.GetRequiredService<AutoStartCoordinator>(),
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<ILogger<WelcomeViewModel>>(),
        DetectSystemLanguage());

    private static Language DetectSystemLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "de" => Language.German,
            "fr" => Language.French,
            _ => Language.English,
        };

    private async Task PurgeHistoryAsync()
    {
        try
        {
            DateTimeOffset cutoff = Services.GetRequiredService<IClock>().UtcNow.AddDays(-HistoryRetentionDays);
            _ = await Services.GetRequiredService<IBreakHistoryRepository>()
                .PurgeOlderThanAsync(cutoff)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.HistoryWriteFailed(_logger!, ex);
        }
        catch (IOException ex)
        {
            AppLog.HistoryWriteFailed(_logger!, ex);
        }
    }

    private async Task SynchronizeAutoStartAsync()
    {
        AutoStartCoordinator coordinator = Services.GetRequiredService<AutoStartCoordinator>();
        try
        {
            _ = await coordinator.SynchronizeFromRegistryAsync().ConfigureAwait(false);
        }
        catch (AutoStartException ex)
        {
            // Autostart ist optional — ein Fehler darf den Start nicht abbrechen.
            AppLog.AutoStartSyncFailed(_logger!, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.AutoStartSyncFailed(_logger!, ex);
        }
        catch (IOException ex)
        {
            AppLog.AutoStartSyncFailed(_logger!, ex);
        }
    }

    private void OpenSettings() => _settingsPresenter?.Show();

    private SettingsViewModel CreateSettingsViewModel() => new(
        Services.GetRequiredService<ISettingsRepository>(),
        Services.GetRequiredService<AutoStartCoordinator>(),
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<ISoundService>(),
        Services.GetRequiredService<IUpdateChecker>(),
        Services.GetRequiredService<IUpdateInstaller>(),
        CreateStatisticsViewModel(),
        Services.GetRequiredService<ILogger<SettingsViewModel>>(),
        GetVersion());

    private StatisticsViewModel CreateStatisticsViewModel() => new(
        Services.GetRequiredService<StatisticsService>(),
        Services.GetRequiredService<IBreakHistoryRepository>(),
        Services.GetRequiredService<CsvExporter>(),
        Services.GetRequiredService<ILocalizationService>());

    /// <summary>
    /// Übernimmt gespeicherte Einstellungen sofort: startet den Timer mit dem
    /// neuen Modell neu und aktualisiert Idle-/Vollbild-Erkennung sowie das Tray.
    /// </summary>
    private void ApplySettingsLive(Settings settings)
    {
        _coordinator?.ApplySchedule(settings);
        _detectionLoop?.ApplySettings(settings);
        RegisterHotkeys(settings);
    }

    private void RequestExit()
    {
        _logService?.LogShutdown(ShutdownReasonUserExit);

        // Vor dem Freigeben der Dienste: laufenden Countdown sichern, damit ein
        // Neustart in derselben Sitzung (z. B. Aktualisierung) ihn fortsetzt.
        PersistTimerSnapshot();

        Dispose();
        Exit();
    }

    /// <summary>
    /// Gibt Tray-Icon, Instanz-Sperre, Hintergrund-Token und alle per DI gehaltenen
    /// Singletons frei. Stellt dabei u. a. die Bildschirmhelligkeit wieder her, gibt
    /// die Hotkeys frei und leert den Log-Puffer. Mehrfachaufrufe sind unschädlich.
    /// </summary>
    public void Dispose()
    {
        _detectionLoop?.Dispose();
        _detectionLoop = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _instanceLock?.Dispose();
        _instanceLock = null;

        (Services as IDisposable)?.Dispose();
    }

    private void OnActivationRequested(object? sender, EventArgs e)
    {
        AppLog.ActivationFromSecondInstance(_logger!);
        // Zweitstart öffnet die Einstellungen — das einzige echte Fenster der
        // Tray-App; das verborgene Hauptfenster bliebe sonst leer.
        OpenSettings();
    }

    private void RegisterGlobalCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnAppDomainUnhandledException(object sender, SystemUnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception && _logService is not null)
        {
            _logService.HandleUnhandledException(exception, CrashSourceAppDomain);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (_logService is not null)
        {
            _logService.HandleUnhandledException(e.Exception, CrashSourceTaskScheduler);
            e.SetObserved();
        }
    }

    private void OnApplicationUnhandledException(object sender, XamlUnhandledExceptionEventArgs e)
    {
        if (_logService is not null)
        {
            _logService.HandleUnhandledException(e.Exception, CrashSourceWinUi);
            e.Handled = true;
        }
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static string GetVersion()
    {
        Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version?.ToString() ?? "0.0.0";
    }
}

/// <summary>
/// Source-generierte Logging-Methoden für die App-Klasse.
/// </summary>
internal static partial class AppLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Warning,
        Message = "Vorheriger Lauf wurde mit einem Crash beendet. Crash-Berichte liegen in logs/crashes/.")]
    public static partial void LastRunCrashed(ILogger logger);

    [LoggerMessage(
        EventId = 1110,
        Level = LogLevel.Warning,
        Message = "Zweite App-Instanz erkannt — bestehende wird benachrichtigt, neue beendet sich.")]
    public static partial void SecondInstanceDetected(ILogger logger);

    [LoggerMessage(
        EventId = 1111,
        Level = LogLevel.Information,
        Message = "Aktivierung durch Zweit-Instanz angefordert.")]
    public static partial void ActivationFromSecondInstance(ILogger logger);

    [LoggerMessage(
        EventId = 1120,
        Level = LogLevel.Information,
        Message = "Tray-Icon ist bereit.")]
    public static partial void TrayReady(ILogger logger);

    [LoggerMessage(
        EventId = 1130,
        Level = LogLevel.Warning,
        Message = "Autostart-Abgleich beim Start fehlgeschlagen — Autostart bleibt unverändert.")]
    public static partial void AutoStartSyncFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1140,
        Level = LogLevel.Warning,
        Message = "Timer-Start beim App-Start fehlgeschlagen — Einstellungen konnten nicht geladen werden.")]
    public static partial void TimerStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1150,
        Level = LogLevel.Warning,
        Message = "Pausen-Historie konnte nicht geschrieben werden — Statistik bleibt unverändert.")]
    public static partial void HistoryWriteFailed(ILogger logger, Exception exception);
}
