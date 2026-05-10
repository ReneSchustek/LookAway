using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Data.Logging;
using LookAway.Data.Power;
using LookAway.Data.Repositories;
using LookAway.Data.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

// Aliase aufloesen Namespace-Kollisionen mit Microsoft.UI.Xaml und System.
using LogService = LookAway.Application.Services.LogService;
using TimerService = LookAway.Application.Services.TimerService;
using XamlUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;
using SystemUnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;

namespace LookAway;

/// <summary>
/// Anwendungs-Bootstrap. Konfiguriert das DI-Container, das Logging
/// (Datei-Sink mit Rotation) und die globalen Crash-Handler.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WinUI-3-XAML-Compiler erfordert eine 'public partial'-App-Klasse fuer den generierten Activator.")]
public partial class App : global::Microsoft.UI.Xaml.Application
{
    private const string AppFolderName = "LookAway";
    private const string LogFolderName = "logs";
    private const string CrashFolderName = "crashes";
    private const string CrashSourceAppDomain = "AppDomain.UnhandledException";
    private const string CrashSourceTaskScheduler = "TaskScheduler.UnobservedTaskException";
    private const string CrashSourceWinUi = "Application.UnhandledException";

    /// <summary>
    /// Globaler Service-Provider, ueber den alle Schichten ihre Abhaengigkeiten beziehen.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;
    private LogService? _logService;
    private ILogger<App>? _logger;

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

        // Power-Mode-Watcher fuer den TimerService starten (Sleep/Resume).
        Services.GetRequiredService<IPowerModeWatcher>().Start();

        bool lastRunCrashed = _logService.LogStart(GetVersion(), Language.German);
        if (lastRunCrashed)
        {
            AppLog.LastRunCrashed(_logger);
        }
    }

    /// <summary>
    /// Wird beim Start der Anwendung aufgerufen und oeffnet das Hauptfenster.
    /// </summary>
    /// <param name="args">Vom System gelieferte Startparameter.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string logDirectory = Path.Combine(appData, AppFolderName, LogFolderName);
        string crashDirectory = Path.Combine(logDirectory, CrashFolderName);

        LogLevel minimumLevel = IsDebugBuild() ? LogLevel.Debug : LogLevel.Information;

        // Sink und Provider werden vom DI-Container erzeugt, damit deren
        // IDisposable-Implementierung bei ServiceProvider.Dispose() greift.
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

        // Timer-Engine
        _ = services.AddSingleton<IClock, SystemClock>();
        _ = services.AddSingleton<IPowerModeWatcher, WindowsPowerModeWatcher>();
        _ = services.AddSingleton<TimerService>();
        _ = services.AddSingleton<ITimerService>(sp => sp.GetRequiredService<TimerService>());

        // Weitere Services werden hier registriert
        // (Tray, Localization, ...)

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
}
