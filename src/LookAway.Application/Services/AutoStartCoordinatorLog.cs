using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Source-generierte Logging-Methoden fuer den <see cref="AutoStartCoordinator"/>.
/// </summary>
/// <remarks>
/// Eingesetzt nach <c>logging.md</c>: vermeidet Boxing und String-Allocation,
/// erfuellt CA1848 und liefert strukturierte Properties (PascalCase).
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
