using System;
using System.IO;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;
using LookAway.Data;
using LookAway.Data.Logging;
using LookAway.Data.Net;
using LookAway.Data.Power;
using LookAway.Data.Repositories;
using LookAway.Data.Services;
using LookAway.Data.Time;
using LookAway.Data.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Baut den DI-Container der Anwendung auf. Ausgelagert aus der App-Klasse, damit
/// diese nur noch den Lebenszyklus verantwortet; die Registrierung aller Dienste
/// ist hier gebündelt.
/// </summary>
internal static class ServiceRegistration
{
    private const string LogFolderName = "logs";
    private const string CrashFolderName = "crashes";

    /// <summary>
    /// Registriert alle Dienste (Logging, Persistenz, Timer-Engine, Update,
    /// Pause-Aktionen, Autostart, Erkennung) und liefert den fertigen Provider.
    /// </summary>
    /// <param name="dataDirectory">Datenverzeichnis (Logs, Einstellungen, Historie).</param>
    /// <param name="appVersion">Laufende Anwendungsversion (für den Update-Vergleich).</param>
    /// <param name="debugLogging">Wahr im Debug-Build: ausführlicheres Log-Level.</param>
    public static ServiceProvider Build(string dataDirectory, Version appVersion, bool debugLogging)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(appVersion);

        ServiceCollection services = new();

        string logDirectory = Path.Combine(dataDirectory, LogFolderName);
        string crashDirectory = Path.Combine(logDirectory, CrashFolderName);
        LogLevel minimumLevel = debugLogging ? LogLevel.Debug : LogLevel.Information;

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

        // Erscheinungsbild (hell, dunkel oder nach Windows)
        _ = services.AddSingleton<ThemeService>();

        // Anwendungsprotokoll für die Protokollansicht
        _ = services.AddSingleton<ILogEntryReader>(_ => new RollingFileLogReader(logDirectory));

        // Sound-Optionen
        _ = services.AddSingleton<ISoundService>(sp =>
            new SoundService(sp.GetRequiredService<ILogger<SoundService>>()));

        // Aufgaben des aufgabenbasierten Modells
        _ = services.AddSingleton<IWorkTaskRepository, JsonWorkTaskRepository>();
        _ = services.AddSingleton<CurrentWorkTaskTracker>();

        // Statistiken / History / CSV
        _ = services.AddSingleton<IBreakHistoryRepository, JsonBreakHistoryRepository>();
        _ = services.AddSingleton<CsvExporter>();
        _ = services.AddSingleton<StatisticsService>();

        // Globale Hotkeys
        _ = services.AddSingleton<IHotkeyService, WindowsHotkeyService>();

        // Pause-Aktionen
        _ = services.AddSingleton<ITopmostWindowGuard, WindowsTopmostWindowGuard>();
        _ = services.AddSingleton<IWindowFrameSuppressor, WindowsWindowFrameSuppressor>();
        _ = services.AddSingleton<IScreenDimmer>(sp => new WindowsScreenDimmer(sp.GetRequiredService<ILogger<WindowsScreenDimmer>>()));
        _ = services.AddSingleton<IMediaController>(sp => new WindowsMediaController(sp.GetRequiredService<ILogger<WindowsMediaController>>()));
        _ = services.AddSingleton<PauseActionService>();

        // Update-Prüfung und automatische Installation
        _ = services.AddSingleton<IHttpGetClient>(sp => new HttpGetClient(sp.GetRequiredService<ILogger<HttpGetClient>>()));
        _ = services.AddSingleton<IUpdateChecker>(sp => new GitHubUpdateChecker(
            sp.GetRequiredService<IHttpGetClient>(),
            appVersion,
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
}
