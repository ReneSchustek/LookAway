using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Logging;
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
using FullscreenDetectionService = LookAway.Application.Services.FullscreenDetectionService;
using IdleDetectionService = LookAway.Application.Services.IdleDetectionService;
using LogService = LookAway.Application.Services.LogService;
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
    Justification = "Die App-Klasse implementiert keinen IDisposable-Vertrag. Disposing der gehaltenen Felder erfolgt im RequestExit-Pfad (Tray) und durch den ServiceProvider beim Process-Shutdown.")]
public partial class App : global::Microsoft.UI.Xaml.Application
{
    private const string AppFolderName = "LookAway";
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

    private Window? _window;
    private LogService? _logService;
    private ILogger<App>? _logger;
    private SingleInstanceLock? _instanceLock;
    private TrayIconService? _trayIcon;
    private CancellationTokenSource? _detectionCts;
    private ReminderPresenter? _reminderPresenter;
    private BreakModel _activeModel = BreakModel.ClassicPomodoro;
    private BreakInterval? _activeInterval;

    /// <summary>
    /// Initialisiert die Anwendung, das DI-Container und die globalen Handler.
    /// </summary>
    public App()
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

    /// <summary>
    /// Wird beim Start aufgerufen. Pruefen Single-Instance, sonst Zeit-
    /// instanz signalisieren und beenden. Bei alleiniger Instanz: Tray-Icon
    /// einblenden und das Hauptfenster verborgen halten.
    /// </summary>
    /// <param name="args">Vom System gelieferte Startparameter.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _instanceLock = new SingleInstanceLock(Environment.UserName);
        if (!_instanceLock.TryAcquire())
        {
            AppLog.SecondInstanceDetected(_logger!);
            _ = _instanceLock.SignalExistingInstance();
            _logService?.LogShutdown(ShutdownReasonSecondInstance);
            Exit();
            return;
        }

        _instanceLock.ActivationRequested += OnActivationRequested;

        _window = new MainWindow();
        // Hauptfenster bleibt verborgen — die App lebt im Tray.
        _window.AppWindow.Hide();

        _reminderPresenter = new ReminderPresenter(_window.DispatcherQueue);

        _trayIcon = new TrayIconService(
            Services.GetRequiredService<ITimerService>(),
            Services.GetRequiredService<TrayStatusPresenter>(),
            _window.DispatcherQueue,
            Services.GetRequiredService<ILogger<TrayIconService>>(),
            ShowMainWindow,
            RequestExit);

        _trayIcon.Show();
        AppLog.TrayReady(_logger!);

        // Registry ist die fuehrende Quelle fuer den Autostart-Zustand: ein
        // manueller Eingriff wird uebernommen, ein veralteter Pfad korrigiert.
        _ = SynchronizeAutoStartAsync();

        // Timer mit dem konfigurierten Modell starten — das Tray-Icon spiegelt
        // den Zustand dann live wider (BRIEF015).
        _ = StartTimerAsync();
    }

    private async Task StartTimerAsync()
    {
        try
        {
            ISettingsRepository repository = Services.GetRequiredService<ISettingsRepository>();
            Settings settings = await repository.LoadAsync().ConfigureAwait(true);
            BreakInterval interval = BreakModelRegistry.GetEffective(settings.BreakModel, settings.CustomDurations);
            _activeModel = settings.BreakModel;
            _activeInterval = interval;

            _trayIcon?.SetActiveModel(settings.BreakModel);
            ITimerService timerService = Services.GetRequiredService<ITimerService>();
            timerService.Start(interval);

            StartDetectionLoop(settings);
            _ = ConsumeTimerEventsAsync(timerService, _detectionCts!.Token);
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
                _trayIcon?.SetDndActive(fullscreen.IsDndActive);

                if (surfaceMissedReminder)
                {
                    // DND wurde beendet: verpasste Erinnerung nachholen.
                    ShowBreakReminder();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartetes Ende beim Shutdown.
        }
    }

    private async Task ConsumeTimerEventsAsync(ITimerService timerService, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TimerEvent timerEvent in timerService.Events.WithCancellation(cancellationToken))
            {
                if (timerEvent is not BreakDueEvent)
                {
                    continue;
                }

                FullscreenDetectionService fullscreen = Services.GetRequiredService<FullscreenDetectionService>();
                if (fullscreen.TryShowReminder())
                {
                    ShowBreakReminder();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartetes Ende beim Shutdown.
        }
    }

    private void ShowBreakReminder() => _reminderPresenter?.Show(_activeModel, OnReminderResult);

    private void OnReminderResult(ReminderResult result)
    {
        if (_activeInterval is null)
        {
            return;
        }

        ITimerService timerService = Services.GetRequiredService<ITimerService>();
        switch (result)
        {
            case ReminderResult.StartBreak:
                // Die Pause laeuft bereits durch die Engine-Transition — nichts zu tun.
                break;
            case ReminderResult.Snooze:
                timerService.Start(BreakInterval.Create(
                    TimeSpan.FromMinutes(BreakReminderViewModel.SnoozeMinutes),
                    _activeInterval.BreakDuration));
                break;
            case ReminderResult.Skip:
                timerService.Start(_activeInterval);
                break;
            default:
                break;
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

    private bool ShowMainWindow()
    {
        if (_window is null)
        {
            return false;
        }

        _ = _window.DispatcherQueue.TryEnqueue(() =>
        {
            _window.AppWindow.Show();
            _window.Activate();
        });
        return true;
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
        Exit();
    }

    private void OnActivationRequested(object? sender, EventArgs e)
    {
        AppLog.ActivationFromSecondInstance(_logger!);
        _ = ShowMainWindow();
    }

    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string logDirectory = Path.Combine(appData, AppFolderName, LogFolderName);
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

        // Autostart (BRIEF004)
        _ = services.AddSingleton<IAutoStartService, RegistryAutoStartService>();
        _ = services.AddSingleton<AutoStartCoordinator>();

        // Tray-Status (BRIEF015)
        _ = services.AddSingleton<TrayStatusPresenter>();

        // Idle-/Vollbild-Erkennung (BRIEF016)
        _ = services.AddSingleton<IIdleDetector, WindowsIdleDetector>();
        _ = services.AddSingleton<IFullscreenDetector, WindowsFullscreenDetector>();
        _ = services.AddSingleton<IdleDetectionService>();
        _ = services.AddSingleton<FullscreenDetectionService>();

        // Timer-Engine (BRIEF005)
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
}
