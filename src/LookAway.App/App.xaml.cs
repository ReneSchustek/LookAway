using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
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
using LookAway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

// Aliase aufloesen Namespace-Kollisionen mit Microsoft.UI.Xaml und System.
using AutoStartCoordinator = LookAway.Application.Services.AutoStartCoordinator;
using BreakReminderViewModel = LookAway.Application.ViewModels.BreakReminderViewModel;
using SettingsViewModel = LookAway.Application.ViewModels.SettingsViewModel;
using StatisticsViewModel = LookAway.Application.ViewModels.StatisticsViewModel;
using WelcomeViewModel = LookAway.Application.ViewModels.WelcomeViewModel;
using StatisticsService = LookAway.Application.Statistics.StatisticsService;
using CsvExporter = LookAway.Application.Statistics.CsvExporter;
using FullscreenDetectionService = LookAway.Application.Services.FullscreenDetectionService;
using IdleDetectionService = LookAway.Application.Services.IdleDetectionService;
using LogService = LookAway.Application.Services.LogService;
using PauseActionService = LookAway.Application.Services.PauseActionService;
using BreakCoordinator = LookAway.Application.Coordination.BreakCoordinator;
using UpdateInstallerService = LookAway.Application.Services.UpdateInstallerService;
using UpdateApplyArgs = LookAway.Application.Services.UpdateApplyArgs;
using StagedUpdate = LookAway.Application.Services.StagedUpdate;
using TrayStatusPresenter = LookAway.Application.Services.TrayStatusPresenter;
using SingleInstanceLock = LookAway.Application.Services.SingleInstanceLock;
using TimerService = LookAway.Application.Services.TimerService;
using XamlUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;
using SystemUnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;

namespace LookAway;

/// <summary>
/// Anwendungs-Bootstrap. Konfiguriert das DI-Container, das Logging
/// (Datei-Sink mit Rotation), die globalen Crash-Handler und das
/// Tray-Icon. Stellt sicher, dass nur eine Instanz pro Benutzer laeuft.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WinUI-3-XAML-Compiler erfordert eine 'public partial'-App-Klasse fuer den generierten Activator.")]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Die App-Klasse implementiert keinen IDisposable-Vertrag. Die gehaltenen Felder und der DI-ServiceProvider werden im RequestExit-Pfad explizit freigegeben.")]
public partial class App : global::Microsoft.UI.Xaml.Application
{
    private const string LogFolderName = "logs";
    private const string CrashFolderName = "crashes";
    private const string CrashSourceAppDomain = "AppDomain.UnhandledException";
    private const string CrashSourceTaskScheduler = "TaskScheduler.UnobservedTaskException";
    private const string CrashSourceWinUi = "Application.UnhandledException";
    private const string ShutdownReasonUserExit = "UserRequested";
    private const string ShutdownReasonSecondInstance = "SecondInstanceDetected";

    /// <summary>
    /// Globaler Service-Provider, ueber den alle Schichten ihre Abhaengigkeiten beziehen.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private const int DetectionPollSeconds = 5;
    private const int HistoryRetentionDays = 365;

    private Window? _window;
    private LogService? _logService;
    private ILogger<App>? _logger;
    private SingleInstanceLock? _instanceLock;
    private TrayIconService? _trayIcon;
    private CancellationTokenSource? _detectionCts;
    private ReminderPresenter? _reminderPresenter;
    private BreakOverlayPresenter? _overlayPresenter;
    private SettingsPresenter? _settingsPresenter;
    private BreakCoordinator? _coordinator;
    private Uri? _updateDownloadUrl;
    private UpdateInfo? _pendingUpdate;

    /// <summary>
    /// Initialisiert die Anwendung, das DI-Container und die globalen Handler.
    /// </summary>
    public App()
    {
        try
        {
            InitializeComponent();
            Services = ConfigureServices();
            _logService = Services.GetRequiredService<LogService>();
            _logger = Services.GetRequiredService<ILogger<App>>();

            RegisterGlobalCrashHandlers();
            UnhandledException += OnApplicationUnhandledException;

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
            // protokolliert. Bestes Bemuehen: in eine Datei schreiben, dann weiter.
            WriteStartupError(ex);
            throw;
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Letzte Diagnose-Chance fuer Startfehler: jede Ausnahme wird in eine Datei geschrieben und anschliessend weitergereicht.")]
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
    /// Wird beim Start aufgerufen. Pruefen Single-Instance, sonst Zeit-
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
            RunUpdateApply(applyArgs);
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

        // Ausstehendes Update beim Start anwenden (Datei-Tausch via Helfer-Prozess);
        // beendet diese Instanz, falls ein Update eingespielt wird.
        if (TryApplyPendingUpdateOnStartup())
        {
            return;
        }

        _instanceLock.ActivationRequested += OnActivationRequested;

        _window = new MainWindow();
        // Hauptfenster bleibt verborgen — die App lebt im Tray.
        _window.AppWindow.Hide();

        _reminderPresenter = new ReminderPresenter(
            _window.DispatcherQueue,
            Services.GetRequiredService<ILocalizationService>());
        _overlayPresenter = new BreakOverlayPresenter(
            _window.DispatcherQueue,
            Services.GetRequiredService<ILocalizationService>());
        _settingsPresenter = new SettingsPresenter(
            _window.DispatcherQueue,
            CreateSettingsViewModel,
            ApplySettingsLive);

        _ = StartAsync();
    }

    /// <summary>
    /// Startsequenz: beim allerersten Start fuehrt der Wizard durch die
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
                    // ohne gueltige Konfiguration wird nicht weitergestartet.
                    _logService?.LogShutdown(ShutdownReasonUserExit);
                    Exit();
                    return;
                }

                settings = await repository.LoadAsync().ConfigureAwait(true);
            }

            // Anzeigesprache aus der Konfiguration uebernehmen.
            Services.GetRequiredService<ILocalizationService>().SetLanguage(settings.Language);

            InitializeTray();

            // Registry ist die fuehrende Quelle fuer den Autostart-Zustand: ein
            // manueller Eingriff wird uebernommen, ein veralteter Pfad korrigiert.
            _ = SynchronizeAutoStartAsync();

            // Alte Historie-Eintraege (>1 Jahr) aufraeumen — nicht startkritisch.
            _ = PurgeHistoryAsync();

            StartTimer(settings);

            // Update-Pruefung im Hintergrund — nicht startkritisch.
            _ = CheckForUpdatesAtStartupAsync(settings);
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

    private async Task CheckForUpdatesAtStartupAsync(Settings settings)
    {
        if (!settings.UpdateCheckEnabled)
        {
            return;
        }

        DateTimeOffset now = Services.GetRequiredService<IClock>().UtcNow;
        if (!UpdateSchedule.IsDue(settings.UpdateCheckFrequency, settings.LastUpdateCheck, now))
        {
            return;
        }

        UpdateInfo info;
        try
        {
            info = await Services.GetRequiredService<IUpdateChecker>()
                .CheckForUpdateAsync()
                .ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // Routinemaessiger Offline-Start darf nicht als unbeobachteter Fehler enden.
            AppLog.UpdateCheckPersistFailed(_logger!, ex);
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            ISettingsRepository repository = Services.GetRequiredService<ISettingsRepository>();
            Settings current = await repository.LoadAsync().ConfigureAwait(true);
            current.LastUpdateCheck = now;
            await repository.SaveAsync(current).ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.UpdateCheckPersistFailed(_logger!, ex);
        }
        catch (IOException ex)
        {
            AppLog.UpdateCheckPersistFailed(_logger!, ex);
        }

        if (info.IsUpdateAvailable)
        {
            _pendingUpdate = info;
            _updateDownloadUrl = info.DownloadUrl;
            _trayIcon?.SetUpdateAvailable(true);

            // Auto-Update: Paket im Hintergrund herunterladen, entpacken und Version
            // + Datei-Hash vermerken; das eigentliche Einspielen erfolgt beim
            // naechsten Start nur fuer genau dieses (verifizierte) Paket.
            if (settings.AutoUpdate && info.PackageUrl is not null)
            {
                _ = AutoStageUpdateAsync(info);
            }
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA1031:Do not catch general exception types",
        Justification = "Das Oeffnen des Browsers ist unkritisch; jeder Fehler wird geloggt, statt die App zu beenden.")]
    private void OpenUpdatePage()
    {
        if (_updateDownloadUrl is null)
        {
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo(_updateDownloadUrl.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.BrowserOpenFailed(_logger!, ex);
        }
    }

    // Tray-Aktion "Update": gefundene Aktualisierung herunterladen, entpacken und
    // ueber den Helfer-Prozess sofort einspielen (App startet danach neu).
    private void OnUpdateRequested() => _ = HandleUpdateRequestedAsync();

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Die manuelle Aktualisierung darf nie abstuerzen: bei jedem Fehler wird auf das Oeffnen der Release-Seite zurueckgefallen.")]
    private async Task HandleUpdateRequestedAsync()
    {
        try
        {
            UpdateInfo? info = _pendingUpdate;
            string target = AppContext.BaseDirectory;

            // Ohne Paket-URL oder bei schreibgeschuetztem Programmordner: Release-Seite oeffnen.
            if (info?.PackageUrl is null || !UpdateInstallerService.IsDirectoryWritable(target))
            {
                OpenUpdatePage();
                return;
            }

            StagedUpdate? staged = await Services.GetRequiredService<UpdateInstallerService>()
                .DownloadAndStageAsync(info)
                .ConfigureAwait(true);
            if (staged is null)
            {
                OpenUpdatePage();
                return;
            }

            // Vom Nutzer angestossen und gerade frisch geladen -> direkt einspielen.
            RelaunchToApply(staged.Directory, target);
        }
        catch (Exception ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
            OpenUpdatePage();
        }
    }

    // Laedt/entpackt das Update im Hintergrund und vermerkt Version + Datei-Hash in
    // den Einstellungen, damit es beim naechsten Start verifiziert eingespielt wird.
    private async Task AutoStageUpdateAsync(UpdateInfo info)
    {
        StagedUpdate? staged = await Services.GetRequiredService<UpdateInstallerService>()
            .DownloadAndStageAsync(info)
            .ConfigureAwait(true);
        if (staged is null)
        {
            return;
        }

        try
        {
            ISettingsRepository repository = Services.GetRequiredService<ISettingsRepository>();
            Settings settings = await repository.LoadAsync().ConfigureAwait(true);
            settings.PendingUpdateVersion = staged.Version;
            settings.PendingUpdateSha256 = staged.ExecutableSha256;
            await repository.SaveAsync(settings).ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.UpdateCheckPersistFailed(_logger!, ex);
        }
        catch (IOException ex)
        {
            AppLog.UpdateCheckPersistFailed(_logger!, ex);
        }
    }

    private void RelaunchToApply(string stagedDir, string target)
    {
        UpdateProcess.StartApply(
            UpdateInstallerService.ExecutablePathIn(stagedDir),
            new UpdateApplyArgs(Environment.ProcessId, stagedDir, target));
        RequestExit();
    }

    /// <summary>
    /// Prueft beim Start auf ein bereits entpacktes, neueres Paket und spielt es
    /// ueber den Helfer-Prozess ein. Gibt <c>true</c> zurueck, wenn diese Instanz
    /// dafuer beendet wird.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Der Start darf nie an der Aktualisierung scheitern: jeder Fehler wird geloggt und der normale Start fortgesetzt.")]
    private bool TryApplyPendingUpdateOnStartup()
    {
        try
        {
            UpdateInstallerService installer = Services.GetRequiredService<UpdateInstallerService>();
            Version current = ParseVersion(GetVersion());
            string target = AppContext.BaseDirectory;

            // Nur ein Update einspielen, das diese Installation selbst vermerkt hat
            // (Version + Datei-Hash) — nie einen einfach untergeschobenen Ordner.
            Settings settings = Services.GetRequiredService<ISettingsRepository>()
                .LoadAsync().GetAwaiter().GetResult();

            string? staged = installer.FindVerifiedPendingUpdateDirectory(
                current, settings.PendingUpdateVersion, settings.PendingUpdateSha256);
            if (staged is null)
            {
                installer.CleanObsolete(current);
                return false;
            }

            if (!UpdateInstallerService.IsDirectoryWritable(target))
            {
                // z. B. "fuer alle Benutzer"-Installation in Programme — kein Auto-Tausch.
                AppLog.UpdateTargetNotWritable(_logger!, target);
                return false;
            }

            AppLog.ApplyingPendingUpdate(_logger!, staged);
            UpdateProcess.StartApply(
                UpdateInstallerService.ExecutablePathIn(staged),
                new UpdateApplyArgs(Environment.ProcessId, staged, target));

            // Lock freigeben und beenden, damit der Helfer die Dateien ersetzen kann.
            _instanceLock?.Dispose();
            _instanceLock = null;
            _logService?.LogShutdown(ShutdownReasonUserExit);
            Exit();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
            return false;
        }
    }

    /// <summary>
    /// Helfer-Modus: wartet auf das Ende der alten Instanz, ersetzt die Dateien im
    /// Zielordner und startet die neue Version. Beendet danach den Helfer-Prozess.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Der Helfer darf nicht abstuerzen; bei Fehlern wird best-effort die installierte App gestartet.")]
    private void RunUpdateApply(UpdateApplyArgs apply)
    {
        _ = Task.Run(() =>
        {
            try
            {
                WaitForProcessExit(apply.Pid, TimeSpan.FromSeconds(30));
                Services.GetRequiredService<UpdateInstallerService>().ApplyStagedFiles(apply.Source, apply.Target);
            }
            catch (Exception ex)
            {
                // Bei Fehler hat ApplyStagedFiles auf den vorigen Stand zurueckgerollt.
                AppLog.UpdateApplyFailed(_logger!, ex);
            }
            finally
            {
                // In jedem Fall die installierte App starten (neuer Stand bei Erfolg,
                // zurueckgerollter, lauffaehiger Stand bei Fehler), dann den Helfer beenden.
                TryStartInstalledApp(apply.Target);
                Environment.Exit(0);
            }
        });
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort-Neustart der App im Helfer-Prozess; ein Fehler darf den Helfer nur am Beenden nicht hindern.")]
    private void TryStartInstalledApp(string targetDir)
    {
        try
        {
            UpdateProcess.StartApp(UpdateInstallerService.ExecutablePathIn(targetDir));
        }
        catch (Exception ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
        }
    }

    private static void WaitForProcessExit(int pid, TimeSpan timeout)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            _ = process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // Prozess existiert nicht mehr — bereits beendet.
        }
        catch (InvalidOperationException)
        {
            // Prozess bereits beendet.
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
            OnUpdateRequested,
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

        _coordinator.ApplySchedule(settings);

        StartDetectionLoop(settings);
        _ = ConsumeTimerEventsAsync(timerService, _detectionCts!.Token);

        RegisterHotkeys(settings);
    }

    private void RegisterHotkeys(Settings settings)
    {
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

    private void StartDetectionLoop(Settings settings)
    {
        IdleDetectionService idle = Services.GetRequiredService<IdleDetectionService>();
        FullscreenDetectionService fullscreen = Services.GetRequiredService<FullscreenDetectionService>();

        idle.IsEnabled = settings.PauseOnIdle;
        idle.Threshold = TimeSpan.FromMinutes(settings.IdleThresholdMinutes);
        fullscreen.IsEnabled = settings.SuppressOnFullscreen;

        _detectionCts = new CancellationTokenSource();
        _ = RunDetectionLoopAsync(idle, fullscreen, _detectionCts.Token);
    }

    private async Task RunDetectionLoopAsync(
        IdleDetectionService idle,
        FullscreenDetectionService fullscreen,
        CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(DetectionPollSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                idle.Evaluate();
                bool surfaceMissedReminder = fullscreen.Evaluate();
                _coordinator?.UpdateDndIndicator(fullscreen.IsDndActive);

                if (surfaceMissedReminder)
                {
                    // DND wurde beendet: verpasste Erinnerung nachholen.
                    _coordinator?.RequestReminder();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartetes Ende beim Shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Tray/Services beim Shutdown bereits freigegeben — unkritisch.
        }
    }

    private async Task ConsumeTimerEventsAsync(ITimerService timerService, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TimerEvent timerEvent in timerService.Events.WithCancellation(cancellationToken))
            {
                _coordinator?.HandleTimerEvent(timerEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartetes Ende beim Shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Container/Services beim Shutdown bereits freigegeben — unkritisch.
        }
    }

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

    private bool OpenSettings()
    {
        _settingsPresenter?.Show();
        return true;
    }

    private SettingsViewModel CreateSettingsViewModel() => new(
        Services.GetRequiredService<ISettingsRepository>(),
        Services.GetRequiredService<AutoStartCoordinator>(),
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<ISoundService>(),
        Services.GetRequiredService<IUpdateChecker>(),
        CreateStatisticsViewModel(),
        Services.GetRequiredService<ILogger<SettingsViewModel>>(),
        GetVersion());

    private StatisticsViewModel CreateStatisticsViewModel() => new(
        Services.GetRequiredService<StatisticsService>(),
        Services.GetRequiredService<IBreakHistoryRepository>(),
        Services.GetRequiredService<CsvExporter>(),
        Services.GetRequiredService<ILocalizationService>());

    /// <summary>
    /// Uebernimmt gespeicherte Einstellungen sofort: startet den Timer mit dem
    /// neuen Modell neu und aktualisiert Idle-/Vollbild-Erkennung sowie das Tray.
    /// </summary>
    private void ApplySettingsLive(Settings settings)
    {
        _coordinator?.ApplySchedule(settings);

        IdleDetectionService idle = Services.GetRequiredService<IdleDetectionService>();
        idle.IsEnabled = settings.PauseOnIdle;
        idle.Threshold = TimeSpan.FromMinutes(settings.IdleThresholdMinutes);

        FullscreenDetectionService fullscreen = Services.GetRequiredService<FullscreenDetectionService>();
        fullscreen.IsEnabled = settings.SuppressOnFullscreen;

        RegisterHotkeys(settings);
    }

    private void RequestExit()
    {
        _logService?.LogShutdown(ShutdownReasonUserExit);
        _detectionCts?.Cancel();
        _detectionCts?.Dispose();
        _detectionCts = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _instanceLock?.Dispose();
        _instanceLock = null;

        // Gibt alle per DI gehaltenen Singletons frei: stellt u. a. die
        // Bildschirmhelligkeit wieder her, gibt Hotkeys frei und leert den Log-Puffer.
        (Services as IDisposable)?.Dispose();

        Exit();
    }

    private void OnActivationRequested(object? sender, EventArgs e)
    {
        AppLog.ActivationFromSecondInstance(_logger!);
        // Zweitstart oeffnet die Einstellungen — das einzige echte Fenster der
        // Tray-App; das verborgene Hauptfenster bliebe sonst leer.
        _ = OpenSettings();
    }

    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        string dataDirectory = AppDataLocation.GetDataDirectory();
        string logDirectory = Path.Combine(dataDirectory, LogFolderName);
        string crashDirectory = Path.Combine(logDirectory, CrashFolderName);

        LogLevel minimumLevel = IsDebugBuild() ? LogLevel.Debug : LogLevel.Information;

        _ = services.AddSingleton(_ => new RollingFileSink(logDirectory));
        _ = services.AddSingleton(sp => new RollingFileLoggerProvider(
            sp.GetRequiredService<RollingFileSink>(),
            minimumLevel,
            ownsSink: false));
        _ = services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<RollingFileLoggerProvider>());

        _ = services.AddLogging(builder =>
        {
            _ = builder.SetMinimumLevel(minimumLevel);
            _ = builder.AddFilter("Microsoft", LogLevel.Warning);
            _ = builder.AddFilter("System", LogLevel.Warning);
        });

        _ = services.AddSingleton<ICrashReporter>(_ => new CrashReporter(crashDirectory));
        _ = services.AddSingleton<LogService>();
        _ = services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();

        // Lokalisierung: Deutsch ist die Referenzsprache.
        _ = services.AddSingleton<ILocalizationService>(_ => new JsonLocalizationService(Language.German));

        // Sound-Optionen
        _ = services.AddSingleton<ISoundService>(sp =>
            new SoundService(sp.GetRequiredService<ILogger<SoundService>>()));

        // Statistiken / History / CSV
        _ = services.AddSingleton<IBreakHistoryRepository, JsonBreakHistoryRepository>();
        _ = services.AddSingleton<CsvExporter>();
        _ = services.AddSingleton<StatisticsService>();

        // Globale Hotkeys
        _ = services.AddSingleton<IHotkeyService, WindowsHotkeyService>();

        // Pause-Aktionen
        _ = services.AddSingleton<IScreenDimmer>(sp => new WindowsScreenDimmer(sp.GetRequiredService<ILogger<WindowsScreenDimmer>>()));
        _ = services.AddSingleton<IMediaController>(sp => new WindowsMediaController(sp.GetRequiredService<ILogger<WindowsMediaController>>()));
        _ = services.AddSingleton<PauseActionService>();

        // Update-Pruefung und automatische Installation
        _ = services.AddSingleton<IHttpGetClient>(sp => new HttpGetClient(sp.GetRequiredService<ILogger<HttpGetClient>>()));
        _ = services.AddSingleton<IUpdateChecker>(sp => new GitHubUpdateChecker(
            sp.GetRequiredService<IHttpGetClient>(),
            ParseVersion(GetVersion()),
            sp.GetRequiredService<ILogger<GitHubUpdateChecker>>()));
        _ = services.AddSingleton<UpdateInstallerService>(sp => new UpdateInstallerService(
            sp.GetRequiredService<IHttpGetClient>(),
            sp.GetRequiredService<ILogger<UpdateInstallerService>>()));

        // Autostart
        _ = services.AddSingleton<IAutoStartService, RegistryAutoStartService>();
        _ = services.AddSingleton<AutoStartCoordinator>();

        // Tray-Status
        _ = services.AddSingleton<TrayStatusPresenter>();

        // Idle-/Vollbild-Erkennung
        _ = services.AddSingleton<IIdleDetector, WindowsIdleDetector>();
        _ = services.AddSingleton<IFullscreenDetector, WindowsFullscreenDetector>();
        _ = services.AddSingleton<IdleDetectionService>();
        _ = services.AddSingleton<FullscreenDetectionService>();

        // Timer-Engine
        _ = services.AddSingleton<IClock, SystemClock>();
        _ = services.AddSingleton<IPowerModeWatcher, WindowsPowerModeWatcher>();
        _ = services.AddSingleton<TimerService>();
        _ = services.AddSingleton<ITimerService>(sp => sp.GetRequiredService<TimerService>());

        return services.BuildServiceProvider();
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
/// Source-generierte Logging-Methoden fuer die App-Klasse.
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
        Message = "Autostart-Abgleich beim Start fehlgeschlagen — Autostart bleibt unveraendert.")]
    public static partial void AutoStartSyncFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1140,
        Level = LogLevel.Warning,
        Message = "Timer-Start beim App-Start fehlgeschlagen — Einstellungen konnten nicht geladen werden.")]
    public static partial void TimerStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1150,
        Level = LogLevel.Warning,
        Message = "Pausen-Historie konnte nicht geschrieben werden — Statistik bleibt unveraendert.")]
    public static partial void HistoryWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1160,
        Level = LogLevel.Warning,
        Message = "Update-Pruefung konnte nicht abgeschlossen werden.")]
    public static partial void UpdateCheckPersistFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1161,
        Level = LogLevel.Warning,
        Message = "Die Update-Seite konnte nicht im Browser geoeffnet werden.")]
    public static partial void BrowserOpenFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1170,
        Level = LogLevel.Information,
        Message = "Ausstehendes Update wird eingespielt: {Directory}.")]
    public static partial void ApplyingPendingUpdate(ILogger logger, string directory);

    [LoggerMessage(
        EventId = 1171,
        Level = LogLevel.Warning,
        Message = "Programmordner {Directory} ist nicht beschreibbar — automatische Aktualisierung nicht moeglich.")]
    public static partial void UpdateTargetNotWritable(ILogger logger, string directory);

    [LoggerMessage(
        EventId = 1172,
        Level = LogLevel.Warning,
        Message = "Automatische Aktualisierung fehlgeschlagen.")]
    public static partial void UpdateApplyFailed(ILogger logger, Exception exception);
}
