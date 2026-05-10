using LookAway.Core.Enums;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Source-generierte Logging-Methoden fuer den <see cref="LogService"/>.
/// </summary>
internal static partial class LogServiceLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "App gestartet: Version {Version}, Sprache {Language}, vorheriger Crash: {LastRunCrashed}.")]
    public static partial void AppStarted(ILogger logger, string version, Language language, bool lastRunCrashed);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "App wird beendet: {Reason}.")]
    public static partial void AppShutdown(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Critical,
        Message = "Unbehandelte Exception aus {Source}.")]
    public static partial void UnhandledException(ILogger logger, Exception exception, string source);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Error,
        Message = "Crash-Bericht konnte nicht persistiert werden.")]
    public static partial void CrashReportFailed(ILogger logger, Exception exception);
}
