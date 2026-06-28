using Microsoft.Extensions.Logging;

namespace LookAway.Data.Services;

/// <summary>
/// Source-generierte Logging-Methoden fuer den
/// <see cref="RegistryAutoStartService"/>.
/// </summary>
/// <remarks>
/// Source-generierte <c>LoggerMessage</c>-Methoden vermeiden Boxing und
/// String-Allocation (CA1848) und liefern strukturierte Properties.
/// </remarks>
internal static partial class RegistryAutoStartServiceLog
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Autostart-Eintrag {ValueName} geschrieben.")]
    public static partial void Enabled(ILogger logger, string valueName);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Autostart-Eintrag {ValueName} entfernt.")]
    public static partial void Disabled(ILogger logger, string valueName);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "Autostart-Eintrag {ValueName} ist bereits aktuell; kein Schreibvorgang noetig.")]
    public static partial void AlreadyCurrent(ILogger logger, string valueName);
}
