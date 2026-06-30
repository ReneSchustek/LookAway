using Microsoft.Extensions.Logging;

namespace LookAway.Services;

/// <summary>
/// Source-generierte Logging-Methoden für den <see cref="TrayIconService"/>.
/// </summary>
internal static partial class TrayIconLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Tray-Icon eingeblendet.")]
    public static partial void IconShown(ILogger logger);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Settings via Tray angefordert.")]
    public static partial void SettingsRequested(ILogger logger);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Pause jetzt via Tray angefordert.")]
    public static partial void StartBreakRequested(ILogger logger);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "Timer-Pause via Tray angefordert.")]
    public static partial void PauseRequested(ILogger logger);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Information, Message = "Timer-Fortsetzen via Tray angefordert.")]
    public static partial void ResumeRequested(ILogger logger);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Information, Message = "Über-Dialog via Tray angefordert.")]
    public static partial void AboutRequested(ILogger logger);

    [LoggerMessage(EventId = 4007, Level = LogLevel.Information, Message = "App-Beenden via Tray angefordert.")]
    public static partial void ExitRequested(ILogger logger);
}
