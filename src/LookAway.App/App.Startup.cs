using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using LookAway.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using CurrentWorkTaskTracker = LookAway.Core.Services.CurrentWorkTaskTracker;
using FullscreenDetectionService = LookAway.Core.Services.FullscreenDetectionService;
using IdleDetectionService = LookAway.Core.Services.IdleDetectionService;
using PauseActionService = LookAway.Core.Services.PauseActionService;
using BreakCoordinator = LookAway.Core.Services.BreakCoordinator;
using BreakHistoryRecorder = LookAway.Core.Services.BreakHistoryRecorder;
using BreakPresenters = LookAway.Core.Services.BreakPresenters;
using UpdateApplyArgs = LookAway.Core.ValueObjects.UpdateApplyArgs;
using TrayStatusPresenter = LookAway.Core.Services.TrayStatusPresenter;
using SingleInstanceLock = LookAway.Data.Services.SingleInstanceLock;

namespace LookAway.App;

/// <summary>
/// Der Start der Anwendung: Instanzprüfung, ausstehendes Update, Startbild, Presenter, Einstellungen
/// samt Wizard, Infobereich, Timer und Hotkeys.
/// </summary>
public sealed partial class LookAwayApp
{
    /// <summary>
    /// Wird beim Start aufgerufen. Läuft schon eine Instanz, bekommt sie ein Signal, und
    /// die neue beendet sich. Sonst erscheint das Tray-Icon, und das Hauptfenster bleibt
    /// verborgen.
    /// </summary>
    /// <param name="args">Vom System gelieferte Startparameter.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Helfer-Modus: aus dem Staging-Ordner gestartet, um nach dem Beenden der
        // alten Instanz die Programmdateien zu ersetzen und neu zu starten.
        if (UpdateApplyArgs.TryParse(Environment.GetCommandLineArgs(), out UpdateApplyArgs applyArgs))
        {
            // Der Helfer beendet den Prozess selbst; hier wartet niemand auf ihn.
            _ = _updateOrchestrator.RunHelperApplyAsync(applyArgs);
            return;
        }

        if (!TryBecomeTheOnlyInstance())
        {
            return;
        }

        // Der Rest des Starts liest die Einstellungen und darf den UI-Thread nicht
        // blockieren; OnLaunched ist ein synchroner Framework-Einstiegspunkt.
        _ = ContinueLaunchAsync();
    }

    // Läuft schon eine Instanz, bekommt sie ein Signal, und diese beendet sich: false.
    private bool TryBecomeTheOnlyInstance()
    {
        _instanceLock = new SingleInstanceLock(Environment.UserName);
        if (!_instanceLock.TryAcquire())
        {
            AppLog.SecondInstanceDetected(_logger);
            _ = _instanceLock.SignalExistingInstance();
            _logService.LogShutdown(ShutdownReasonSecondInstance);
            Exit();
            return false;
        }

        // Gleich nach dem Erwerb der Sperre: Eine Aufforderung des Setups darf auch mitten im Start
        // nicht verloren gehen. Beendet wird auf dem UI-Thread, wie über das Tray-Menü.
        DispatcherQueue launchQueue = DispatcherQueue.GetForCurrentThread();
        _instanceLock.QuitRequested += (_, _) => launchQueue.TryEnqueue(RequestExit);
        return true;
    }

    private async Task ContinueLaunchAsync()
    {
        // Ausstehendes Update beim Start anwenden (Datei-Tausch via Helfer-Prozess);
        // beendet diese Instanz, falls ein Update eingespielt wird.
        if (await _updateOrchestrator.TryApplyPendingUpdateOnStartupAsync().ConfigureAwait(true))
        {
            ExitForUpdate();
            return;
        }

        _instanceLock!.ActivationRequested += OnActivationRequested; // OnLaunched setzt die Sperre, bevor es hierher verzweigt

        // Erst nach der Update-Prüfung: Spielt der Start ein Update ein, endet er gleich wieder,
        // und ein Startbild bliebe nur ein Aufblitzen.
        _splash = new SplashPresenter(() => new Views.SplashWindow(GetVersion()));
        await _splash.ShowAsync().ConfigureAwait(true);

        CreatePresenters();
        await StartAsync().ConfigureAwait(true);
    }

    // Das verborgene Hauptfenster stellt den Fenster-Thread; darüber entstehen die Presenter.
    private void CreatePresenters()
    {
        _window = new MainWindow();
        // Hauptfenster bleibt verborgen — die App lebt im Tray.
        _window.AppWindow.Hide();

        _ui = new DispatcherQueueUi(_window.DispatcherQueue);
        ThemeService themeService = Services.GetRequiredService<ThemeService>();
        ILocalizationService localization = Services.GetRequiredService<ILocalizationService>();

        _reminderPresenter = new ReminderPresenter(
            _ui,
            viewModel => new WinUiAppWindow(new Views.BreakReminderWindow(viewModel, localization), themeService),
            Services.GetRequiredService<ILogger<ReminderPresenter>>());
        _overlayPresenter = CreateOverlayPresenter(_window.DispatcherQueue);
        _settingsPresenter = new SettingsPresenter(
            _ui,
            CreateSettingsViewModel,
            viewModel => new WinUiAppWindow(new Views.SettingsWindow(viewModel), themeService),
            ApplySettingsLive,
            SuspendHotkeysWhileCapturing,
            RefreshCurrentWorkTask);
    }

    private BreakOverlayPresenter CreateOverlayPresenter(DispatcherQueue dispatcher) => new(
        new BreakOverlayWindows(
            dispatcher,
            Services.GetRequiredService<ILocalizationService>(),
            Services.GetRequiredService<ITopmostWindowGuard>(),
            Services.GetRequiredService<IWindowFrameSuppressor>(),
            Services.GetRequiredService<ILogger<BreakOverlayWindows>>()),
        Services.GetRequiredService<IOverlayImageInspector>(),
        Services.GetRequiredService<ILogger<BreakOverlayPresenter>>());

    // Lock freigeben und beenden, damit der Helfer die Dateien ersetzen kann.
    private void ExitForUpdate()
    {
        _instanceLock?.Dispose();
        _instanceLock = null;
        _logService.LogShutdown(ShutdownReasonUserExit);
        Exit();
    }

    /// <summary>
    /// Startsequenz: beim allerersten Start führt der Wizard durch die
    /// Erstkonfiguration; danach werden Tray und Timer eingerichtet.
    /// </summary>
    private async Task StartAsync()
    {
        try
        {
            Settings? settings = await LoadSettingsOrRunWizardAsync().ConfigureAwait(true);
            if (settings is null)
            {
                return;
            }

            ApplyStartupSettings(settings);
            InitializeTray();
            StartBackgroundWork();
            StartTimer(settings);

            // Update-Prüfung im Hintergrund — nicht startkritisch.
            _ = _updateOrchestrator.CheckAtStartupAsync(settings, _shutdown.Token);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            AppLog.TimerStartFailed(_logger, ex);
        }
        finally
        {
            await CloseSplashAsync().ConfigureAwait(true);
        }
    }

    // Beim allerersten Start führt der Wizard durch die Erstkonfiguration. Schließt der Benutzer
    // ihn ohne Abschluss, wird ohne gültige Konfiguration nicht weitergestartet: null.
    private async Task<Settings?> LoadSettingsOrRunWizardAsync()
    {
        ISettingsRepository repository = Services.GetRequiredService<ISettingsRepository>();
        Settings settings = await repository.LoadAsync().ConfigureAwait(true);
        if (!settings.IsFirstRun)
        {
            return settings;
        }

        // Der Wizard wartet auf den Benutzer; das Startbild darf nicht über ihm stehen bleiben.
        await CloseSplashAsync().ConfigureAwait(true);
        if (!await ShowWelcomeAsync().ConfigureAwait(true))
        {
            _logService.LogShutdown(ShutdownReasonUserExit);
            Exit();
            return null;
        }

        return await repository.LoadAsync().ConfigureAwait(true);
    }

    // Anzeigesprache und Erscheinungsbild aus der Konfiguration übernehmen, bevor das erste
    // Fenster aufgebaut wird.
    private void ApplyStartupSettings(Settings settings)
    {
        Services.GetRequiredService<ILocalizationService>().SetLanguage(settings.Language);
        Services.GetRequiredService<ThemeService>().SetTheme(settings.AppTheme);
        AppLog.SettingsLoaded(_logger, settings.Language, settings.BreakModel);
    }

    // Nicht startkritische Arbeit, die im Hintergrund läuft.
    private void StartBackgroundWork()
    {
        // Registry ist die führende Quelle für den Autostart-Zustand: ein
        // manueller Eingriff wird übernommen, ein veralteter Pfad korrigiert.
        _ = SynchronizeAutoStartAsync();

        // Alte Historie-Einträge (>1 Jahr) aufräumen.
        _ = PurgeHistoryAsync();

        // Laufende Aufgabe einmal bestimmen, damit sie beim ersten Pausenende steht.
        _ = RefreshCurrentWorkTaskAsync();
    }

    private Task CloseSplashAsync() => _splash?.CloseAsync() ?? Task.CompletedTask;

    private void InitializeTray()
    {
        _trayIcon = new TrayIconService(
            Services.GetRequiredService<ITimerService>(),
            Services.GetRequiredService<TrayStatusPresenter>(),
            Services.GetRequiredService<ILocalizationService>(),
            _window!.DispatcherQueue, // ContinueLaunchAsync legt das Fenster an, bevor der Start weiterläuft
            Services.GetRequiredService<ILogger<TrayIconService>>(),
            new TrayActions(
                OpenSettings,
                RequestExit,
                _updateOrchestrator.HandleManualUpdateRequested,
                () => _coordinator?.RequestReminder()));

        _trayIcon.Show();
        AppLog.TrayReady(_logger);
    }

    // Timer mit dem konfigurierten Modell starten — das Tray-Icon spiegelt den
    // Zustand dann live wider.
    private void StartTimer(Settings settings)
    {
        ITimerService timerService = Services.GetRequiredService<ITimerService>();
        _coordinator = CreateCoordinator(timerService);

        // Innerhalb derselben Windows-Sitzung (z. B. nach einer Aktualisierung) den
        // Countdown fortsetzen statt zurückzusetzen; bei einem Windows-Neustart
        // (neue Sitzung) startet er regulär neu.
        _coordinator.ApplySchedule(settings, CreateTimerResumption().Take(settings.BreakModel));

        _detectionLoop = new DetectionLoopHost(
            Services.GetRequiredService<IdleDetectionService>(),
            Services.GetRequiredService<FullscreenDetectionService>(),
            timerService,
            _coordinator);
        _detectionLoop.ApplySettings(settings);
        _detectionLoop.Start();

        _hotkeys = CreateHotkeyBindings();
        _hotkeys.Apply(settings);
    }

    private BreakCoordinator CreateCoordinator(ITimerService timerService) => new(
        timerService,
        new BreakPresenters(
            _reminderPresenter!, // beide Presenter entstehen in ContinueLaunchAsync vor StartAsync
            _overlayPresenter!,
            _trayIcon!), // InitializeTray läuft in StartAsync vor StartTimer
        Services.GetRequiredService<PauseActionService>(),
        Services.GetRequiredService<ISoundService>(),
        Services.GetRequiredService<IClock>(),
        new BreakHistoryRecorder(
            Services.GetRequiredService<IBreakHistoryRepository>(),
            Services.GetRequiredService<ILogger<BreakHistoryRecorder>>(),
            Services.GetRequiredService<CurrentWorkTaskTracker>()),
        Services.GetRequiredService<FullscreenDetectionService>());

    private HotkeyBindings CreateHotkeyBindings() => new(
        Services.GetRequiredService<IHotkeyService>(),
        _ui!, // ContinueLaunchAsync legt den Fenster-Zugang an, bevor der Start weiterläuft
        () => _coordinator?.RequestReminder(),
        () => _coordinator?.SkipOrSnooze(),
        () => _coordinator?.ToggleManualDnd());

    private Task<bool> ShowWelcomeAsync()
    {
        ThemeService themeService = Services.GetRequiredService<ThemeService>();
        WelcomePresenter presenter = new(
            _ui!, // ContinueLaunchAsync legt den Fenster-Zugang an, bevor der Start weiterläuft
            CreateWelcomeViewModel,
            viewModel => new WinUiAppWindow(new Views.WelcomeWindow(viewModel), themeService),
            _ => { });
        return presenter.ShowAsync();
    }
}
