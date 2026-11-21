using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Data;
using LookAway.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

// Aliase auflösen Namespace-Kollisionen mit Microsoft.UI.Xaml und System.
using LogService = LookAway.Core.Services.LogService;
using BreakCoordinator = LookAway.Core.Services.BreakCoordinator;
using UpdateApplyArgs = LookAway.Core.ValueObjects.UpdateApplyArgs;
using SingleInstanceLock = LookAway.Data.Services.SingleInstanceLock;
using XamlUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;
using SystemUnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;

namespace LookAway.App;

/// <summary>
/// Anwendungs-Bootstrap. Konfiguriert den DI-Container, das Logging
/// (Datei-Sink mit Rotation), die globalen Crash-Handler und das
/// Tray-Icon. Je Benutzer läuft nur eine Instanz; eine zweite öffnet in der ersten die
/// Einstellungen und beendet sich.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Der WinUI-3-XAML-Compiler erzeugt die zweite Partialklasse als 'public'; ein abweichender Modifizierer ist nicht kompilierbar.")]
[ExcludeFromCodeCoverage(Justification = "Composition Root und Lebenszyklus der WinUI-Anwendung. Die Entscheidungen stehen in prüfbaren Klassen (UpdateOrchestrator, TimerResumption, HotkeyBindings, SystemLanguage, Presentern); den Container prüft ServiceRegistrationTests.")]
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
    private static IServiceProvider Services { get; set; } = null!; // im Konstruktor gesetzt, bevor irgendetwas sie liest

    // Ein Jahr: Weiter reicht die Jahresstatistik nie zurück, und die Historie bleibt
    // klein genug, um bei jedem Start ganz gelesen zu werden.
    private const int HistoryRetentionDays = 365;

    private Window? _window;
    // Diese drei setzt der Konstruktor; scheitert er, gibt es keine Anwendung, die sie bräuchte.
    private readonly LogService _logService;
    private readonly ILogger<LookAwayApp> _logger;
    private SingleInstanceLock? _instanceLock;
    private TrayIconService? _trayIcon;
    private DetectionLoopHost? _detectionLoop;
    private ReminderPresenter? _reminderPresenter;
    private BreakOverlayPresenter? _overlayPresenter;
    private SettingsPresenter? _settingsPresenter;
    private SplashPresenter? _splash;
    private DispatcherQueueUi? _ui;

    // Zuletzt angewendete Einstellungen — Grundlage, um die Hotkeys nach einer
    // Aufnahme wieder in genau dem Zustand zu registrieren, der vorher galt.
    private HotkeyBindings? _hotkeys;
    private BreakCoordinator? _coordinator;
    private readonly UpdateOrchestrator _updateOrchestrator;

    // Wird beim Beenden ausgelöst und beendet die Vorgänge, die im Hintergrund
    // weiterlaufen — allen voran die Update-Prüfung beim Start. Ohne sie liefe die
    // weiter, während der Container schon abgebaut wird; ihr Zugriff auf die dann
    // entsorgte Ablage endete in einer Ausnahme, die niemand mehr beobachtet.
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>
    /// Initialisiert die Anwendung, den DI-Container und die globalen Handler.
    /// </summary>
    public LookAwayApp()
    {
        try
        {
            InitializeComponent();

            Services = BuildServices();
            _logService = Services.GetRequiredService<LogService>();
            _logger = Services.GetRequiredService<ILogger<LookAwayApp>>();

            RegisterGlobalCrashHandlers();
            UnhandledException += OnApplicationUnhandledException;

            _updateOrchestrator = CreateUpdateOrchestrator();

            Services.GetRequiredService<IPowerModeWatcher>().Start();

            if (_logService.LogStart(GetVersion()))
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

    // Update-Helfer: Er startet aus dem Staging-Ordner, bedient aber die Installation im
    // Zielordner. Sein Datenort muss deshalb der der Installation sein — sonst läse er
    // Einstellungen und Logs neben sich und fände den vermerkten Datei-Hash nicht, mit dem er
    // die Quelle prüft. Muss vor dem Aufbau der Dienste geschehen.
    private static ServiceProvider BuildServices()
    {
        if (UpdateApplyArgs.TryParse(Environment.GetCommandLineArgs(), out UpdateApplyArgs helperArgs))
        {
            AppDataLocation.UseBaseDirectory(helperArgs.Target);
        }

        return ServiceRegistration.Build(AppDataLocation.GetDataDirectory(), ParseVersion(GetVersion()), IsDebugBuild());
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

    private static Version ParseVersion(string version)
        => Version.TryParse(version, out Version? parsed) ? parsed : new Version(0, 0, 0);

    private void OpenSettings() => _settingsPresenter?.Show();

    private void RequestExit()
    {
        _logService.LogShutdown(ShutdownReasonUserExit);

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
        // Zuerst abbrechen, dann abbauen: Was im Hintergrund läuft, soll aufhören,
        // bevor ihm die Dienste unter den Händen weggeräumt werden.
        _shutdown.Cancel();

        _detectionLoop?.Dispose();
        _detectionLoop = null;
        _hotkeys?.Dispose();
        _hotkeys = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _instanceLock?.Dispose();
        _instanceLock = null;

        (Services as IDisposable)?.Dispose();
        _shutdown.Dispose();
    }

    private void OnActivationRequested(object? sender, EventArgs e)
    {
        AppLog.ActivationFromSecondInstance(_logger);
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
        if (e.ExceptionObject is Exception exception)
        {
            _logService.HandleUnhandledException(exception, CrashSourceAppDomain);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logService.HandleUnhandledException(e.Exception, CrashSourceTaskScheduler);
        e.SetObserved();
    }

    private void OnApplicationUnhandledException(object sender, XamlUnhandledExceptionEventArgs e)
    {
        _logService.HandleUnhandledException(e.Exception, CrashSourceWinUi);
        e.Handled = true;
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
        EventId = 1121,
        Level = LogLevel.Information,
        Message = "Einstellungen geladen: Sprache {Language}, Pausenmodell {BreakModel}.")]
    public static partial void SettingsLoaded(ILogger logger, Language language, BreakModel breakModel);

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
        EventId = 1151,
        Level = LogLevel.Warning,
        Message = "Laufende Aufgabe konnte nicht bestimmt werden — Aufgabenliste nicht lesbar.")]
    public static partial void CurrentTaskRefreshFailed(ILogger logger, Exception exception);
}
