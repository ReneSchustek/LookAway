using System;
using System.ComponentModel;
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
using LookAway.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

// Aliase auflösen Namespace-Kollisionen mit Microsoft.UI.Xaml und System.
using AutoStartCoordinator = LookAway.Core.Services.AutoStartCoordinator;
using BreakReminderViewModel = LookAway.App.ViewModels.BreakReminderViewModel;
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
using StagedUpdate = LookAway.Core.ValueObjects.StagedUpdate;
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
    private const string CrashFolderName = "crashes";
    private const string CrashSourceAppDomain = "AppDomain.UnhandledException";
    private const string CrashSourceTaskScheduler = "TaskScheduler.UnobservedTaskException";
    private const string CrashSourceWinUi = "Application.UnhandledException";
    private const string ShutdownReasonUserExit = "UserRequested";
    private const string ShutdownReasonSecondInstance = "SecondInstanceDetected";

    // Service-Provider der Composition Root. Bewusst privat: außerhalb dieser Klasse
    // werden Abhängigkeiten per Konstruktor injiziert, nicht nachgeschlagen.
    private static IServiceProvider Services { get; set; } = null!;

    private const int DetectionPollSeconds = 5;
    private const int HistoryRetentionDays = 365;

    private Window? _window;
    private LogService? _logService;
    private ILogger<LookAwayApp>? _logger;
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
    public LookAwayApp()
    {
        try
        {
            InitializeComponent();
            Services = ConfigureServices();
            _logService = Services.GetRequiredService<LogService>();
            _logger = Services.GetRequiredService<ILogger<LookAwayApp>>();

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
            // protokolliert. Bestes Bemühen: in eine Datei schreiben, dann weiter.
            WriteStartupError(ex);
            throw;
        }
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

        // Der Rest des Starts liest die Einstellungen und darf den UI-Thread nicht
        // blockieren; OnLaunched ist ein synchroner Framework-Einstiegspunkt.
        _ = ContinueLaunchAsync();
    }

    private async Task ContinueLaunchAsync()
    {
        // Ausstehendes Update beim Start anwenden (Datei-Tausch via Helfer-Prozess);
        // beendet diese Instanz, falls ein Update eingespielt wird.
        if (await TryApplyPendingUpdateOnStartupAsync().ConfigureAwait(true))
        {
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
            ApplySettingsLive);

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
            // Routinemäßiger Offline-Start darf nicht als unbeobachteter Fehler enden.
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
            // nächsten Start nur für genau dieses (verifizierte) Paket.
            if (settings.AutoUpdate && info.PackageUrl is not null && !IsAlreadyStaged(info, settings))
            {
                _ = AutoStageUpdateAsync(info);
            }
        }
    }

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
        // Fehlt ein Standardbrowser oder verweigert die Shell den Start, meldet
        // Process.Start genau diese Typen; die App läuft unbeeindruckt weiter.
        catch (Win32Exception ex)
        {
            AppLog.BrowserOpenFailed(_logger!, ex);
        }
        catch (InvalidOperationException ex)
        {
            AppLog.BrowserOpenFailed(_logger!, ex);
        }
    }

    // Tray-Aktion "Update": gefundene Aktualisierung herunterladen, entpacken und
    // über den Helfer-Prozess sofort einspielen (App startet danach neu).
    private void OnUpdateRequested() => _ = HandleUpdateRequestedAsync();

    private async Task HandleUpdateRequestedAsync()
    {
        try
        {
            UpdateInfo? info = _pendingUpdate;
            string target = AppContext.BaseDirectory;

            // Ohne Paket-URL oder bei schreibgeschütztem Programmordner: Release-Seite öffnen.
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
        // Datei- und Prozessfehler beim Bereitstellen: auf die Release-Seite zurückfallen.
        catch (IOException ex)
        {
            OnManualUpdateFailed(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            OnManualUpdateFailed(ex);
        }
        catch (Win32Exception ex)
        {
            OnManualUpdateFailed(ex);
        }
        catch (InvalidOperationException ex)
        {
            OnManualUpdateFailed(ex);
        }
    }

    private void OnManualUpdateFailed(Exception exception)
    {
        AppLog.UpdateApplyFailed(_logger!, exception);
        OpenUpdatePage();
    }

    // Lädt/entpackt das Update im Hintergrund und vermerkt Version + Datei-Hash in
    // den Einstellungen, damit es beim nächsten Start verifiziert eingespielt wird.
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

    /// <summary>
    /// Liegt für genau diese Aktualisierung bereits ein verifiziertes Paket im
    /// Staging-Ordner? Dann nicht erneut laden. Sonst würde die entpackte EXE bei
    /// jedem Start neu geschrieben und vom Virenscanner erneut geprüft — solange
    /// dieser Scan das Einspielen (kurzzeitig „Zugriff verweigert") blockiert, käme
    /// das Update nie durch. Ein einmal gestagetes Paket „kühlt ab" und wird beim
    /// nächsten Start eingespielt.
    /// </summary>
    private static bool IsAlreadyStaged(UpdateInfo info, Settings settings)
    {
        if (!string.Equals(settings.PendingUpdateVersion, info.LatestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Version current = ParseVersion(GetVersion());
        return Services.GetRequiredService<UpdateInstallerService>()
            .FindVerifiedPendingUpdateDirectory(current, settings.PendingUpdateVersion, settings.PendingUpdateSha256)
            is not null;
    }

    private void RelaunchToApply(string stagedDir, string target)
    {
        UpdateProcess.StartApply(
            UpdateInstallerService.ExecutablePathIn(stagedDir),
            new UpdateApplyArgs(Environment.ProcessId, stagedDir, target));
        RequestExit();
    }

    /// <summary>
    /// Prüft beim Start auf ein bereits entpacktes, neueres Paket und spielt es
    /// über den Helfer-Prozess ein. Gibt <c>true</c> zurück, wenn diese Instanz
    /// dafür beendet wird.
    /// </summary>
    private async Task<bool> TryApplyPendingUpdateOnStartupAsync()
    {
        try
        {
            UpdateInstallerService installer = Services.GetRequiredService<UpdateInstallerService>();
            Version current = ParseVersion(GetVersion());
            string target = AppContext.BaseDirectory;

            // Nur ein Update einspielen, das diese Installation selbst vermerkt hat
            // (Version + Datei-Hash) — nie einen einfach untergeschobenen Ordner.
            Settings settings = await Services.GetRequiredService<ISettingsRepository>()
                .LoadAsync().ConfigureAwait(true);

            string? staged = installer.FindVerifiedPendingUpdateDirectory(
                current, settings.PendingUpdateVersion, settings.PendingUpdateSha256);
            if (staged is null)
            {
                installer.CleanObsolete(current);
                return false;
            }

            if (!UpdateInstallerService.IsDirectoryWritable(target))
            {
                // z. B. "für alle Benutzer"-Installation in Programme — kein Auto-Tausch.
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
        // Ein fehlgeschlagenes Update darf den Start nie verhindern: protokollieren
        // und regulär weiterstarten.
        catch (IOException ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
            return false;
        }
        catch (Win32Exception ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
            return false;
        }
    }

    /// <summary>
    /// Helfer-Modus: wartet auf das Ende der alten Instanz, ersetzt die Dateien im
    /// Zielordner und startet die neue Version. Beendet danach den Helfer-Prozess.
    /// </summary>
    private void RunUpdateApply(UpdateApplyArgs apply)
    {
        _ = Task.Run(() =>
        {
            try
            {
                WaitForProcessExit(apply.Pid, TimeSpan.FromSeconds(30));
                Services.GetRequiredService<UpdateInstallerService>().ApplyStagedFiles(apply.Source, apply.Target);
            }
            // Bei Fehler hat ApplyStagedFiles auf den vorigen Stand zurückgerollt.
            catch (IOException ex)
            {
                AppLog.UpdateApplyFailed(_logger!, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                AppLog.UpdateApplyFailed(_logger!, ex);
            }
            catch (InvalidOperationException ex)
            {
                AppLog.UpdateApplyFailed(_logger!, ex);
            }
            finally
            {
                // In jedem Fall die installierte App starten (neuer Stand bei Erfolg,
                // zurückgerollter, lauffähiger Stand bei Fehler), dann den Helfer beenden.
                TryStartInstalledApp(apply.Target);
                Environment.Exit(0);
            }
        });
    }

    private void TryStartInstalledApp(string targetDir)
    {
        try
        {
            UpdateProcess.StartApp(UpdateInstallerService.ExecutablePathIn(targetDir));
        }
        // Der Helfer beendet sich anschließend ohnehin; ein fehlgeschlagener Neustart
        // wird nur protokolliert.
        catch (Win32Exception ex)
        {
            AppLog.UpdateApplyFailed(_logger!, ex);
        }
        catch (InvalidOperationException ex)
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

        // Innerhalb derselben Windows-Sitzung (z. B. nach einer Aktualisierung) den
        // Countdown fortsetzen statt zurückzusetzen; bei einem Windows-Neustart
        // (neue Sitzung) startet er regulär neu.
        _coordinator.ApplySchedule(settings, LoadResumeRemaining(settings));

        StartDetectionLoop(settings);
        _ = ConsumeTimerEventsAsync(timerService, _detectionCts!.Token);

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
        _detectionCts?.Cancel();
        _detectionCts?.Dispose();
        _detectionCts = null;
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

        // Timer-Momentaufnahme: setzt den Countdown nach einem Neustart in derselben
        // Windows-Sitzung (z. B. Aktualisierung) fort.
        _ = services.AddSingleton<ITimerStateStore, JsonTimerStateStore>();

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

        // Update-Prüfung und automatische Installation
        _ = services.AddSingleton<IHttpGetClient>(sp => new HttpGetClient(sp.GetRequiredService<ILogger<HttpGetClient>>()));
        _ = services.AddSingleton<IUpdateChecker>(sp => new GitHubUpdateChecker(
            sp.GetRequiredService<IHttpGetClient>(),
            ParseVersion(GetVersion()),
            sp.GetRequiredService<ILogger<GitHubUpdateChecker>>()));
        _ = services.AddSingleton<UpdateInstallerService>(sp => new UpdateInstallerService(
            sp.GetRequiredService<IHttpGetClient>(),
            sp.GetRequiredService<ILogger<UpdateInstallerService>>()));
        // Schmale Sicht für das Settings-ViewModel (Ein-Klick-Installation).
        _ = services.AddSingleton<IUpdateInstaller>(sp => sp.GetRequiredService<UpdateInstallerService>());

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

    [LoggerMessage(
        EventId = 1160,
        Level = LogLevel.Warning,
        Message = "Update-Prüfung konnte nicht abgeschlossen werden.")]
    public static partial void UpdateCheckPersistFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1161,
        Level = LogLevel.Warning,
        Message = "Die Update-Seite konnte nicht im Browser geöffnet werden.")]
    public static partial void BrowserOpenFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1170,
        Level = LogLevel.Information,
        Message = "Ausstehendes Update wird eingespielt: {Directory}.")]
    public static partial void ApplyingPendingUpdate(ILogger logger, string directory);

    [LoggerMessage(
        EventId = 1171,
        Level = LogLevel.Warning,
        Message = "Programmordner {Directory} ist nicht beschreibbar — automatische Aktualisierung nicht möglich.")]
    public static partial void UpdateTargetNotWritable(ILogger logger, string directory);

    [LoggerMessage(
        EventId = 1172,
        Level = LogLevel.Warning,
        Message = "Automatische Aktualisierung fehlgeschlagen.")]
    public static partial void UpdateApplyFailed(ILogger logger, Exception exception);
}
