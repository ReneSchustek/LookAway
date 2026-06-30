using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Coordinator für das Lifecycle-Logging der Anwendung.
/// </summary>
/// <remarks>
/// Kapselt drei wiederkehrende Operationen:
/// <list type="bullet">
/// <item>Beim Start einen strukturierten Eintrag mit Version und Sprache schreiben.</item>
/// <item>Beim Shutdown einen passenden Gegen-Eintrag schreiben.</item>
/// <item>Beim Start prüfen, ob im letzten Lauf ein Crash protokolliert wurde.</item>
/// </list>
/// Der Service ist UI-frei und damit unit-testbar.
/// </remarks>
public sealed class LogService
{
    private readonly ILogger<LogService> _logger;
    private readonly ICrashReporter _crashReporter;

    /// <summary>
    /// Erzeugt einen LogService mit den nötigen Abhängigkeiten.
    /// </summary>
    public LogService(ILogger<LogService> logger, ICrashReporter crashReporter)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(crashReporter);

        _logger = logger;
        _crashReporter = crashReporter;
    }

    /// <summary>
    /// Schreibt einen Start-Eintrag und gibt zurück, ob der vorherige
    /// Lauf in einen Crash gemündet ist.
    /// </summary>
    /// <param name="version">Versionskennung der Anwendung (z. B. <c>1.0.0</c>).</param>
    /// <param name="language">Aktuelle Anzeigesprache.</param>
    /// <returns>
    /// <c>true</c>, wenn seit dem letzten Acknowledgement ein neuer
    /// Crash-Bericht vorliegt; sonst <c>false</c>.
    /// </returns>
    public bool LogStart(string version, Language language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        bool lastRunCrashed = _crashReporter.HasUnresolvedCrashes();
        LogServiceLog.AppStarted(_logger, version, language, lastRunCrashed);
        return lastRunCrashed;
    }

    /// <summary>
    /// Schreibt einen Shutdown-Eintrag.
    /// </summary>
    /// <param name="reason">Grund für das Beenden (z. B. <c>UserRequested</c>).</param>
    public void LogShutdown(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        LogServiceLog.AppShutdown(_logger, reason);
    }

    /// <summary>
    /// Schreibt eine unbehandelte Exception über den injizierten
    /// <see cref="ICrashReporter"/> persistent weg und legt einen
    /// strukturierten Log-Eintrag dazu an.
    /// </summary>
    /// <param name="exception">Die unbehandelte Exception.</param>
    /// <param name="source">Quelle des Crashes.</param>
    public void HandleUnhandledException(Exception exception, string source)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        try
        {
            _crashReporter.Report(exception, source);
        }
        catch (IOException ex)
        {
            // CrashReporter selber ist robust; falls hier doch etwas durchschlägt, loggen wir es.
            LogServiceLog.CrashReportFailed(_logger, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogServiceLog.CrashReportFailed(_logger, ex);
        }

        LogServiceLog.UnhandledException(_logger, exception, source);
    }

    /// <summary>
    /// Markiert die bestehenden Crashes als vom Benutzer gesehen.
    /// </summary>
    public void AcknowledgeCrashes() => _crashReporter.MarkResolved();
}
