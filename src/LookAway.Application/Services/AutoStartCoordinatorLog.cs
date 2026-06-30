using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Source-generierte Logging-Methoden für den <see cref="AutoStartCoordinator"/>.
/// </summary>
/// <remarks>
/// Source-generierte <c>LoggerMessage</c>-Methoden vermeiden Boxing und
/// String-Allocation (CA1848) und liefern strukturierte Properties.
/// </remarks>
internal static partial class AutoStartCoordinatorLog
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "Autostart-Einstellung angewendet: Enabled={Enabled}.")]
    public static partial void SettingApplied(ILogger logger, bool enabled);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Information,
        Message = "Autostart-Einstellung an Registry angeglichen: Enabled={Enabled}.")]
    public static partial void SettingsAlignedToRegistry(ILogger logger, bool enabled);
}
