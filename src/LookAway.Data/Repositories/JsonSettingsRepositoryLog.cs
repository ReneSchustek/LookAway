using Microsoft.Extensions.Logging;

namespace LookAway.Data.Repositories;

/// <summary>
/// Source-generierte Logging-Methoden fuer das <see cref="JsonSettingsRepository"/>.
/// </summary>
/// <remarks>
/// Eingesetzt nach <c>logging.md</c>: vermeidet Boxing und String-Allocation,
/// erfuellt CA1848 / CA1873 und sorgt fuer strukturierte Properties (PascalCase).
/// </remarks>
internal static partial class JsonSettingsRepositoryLog
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Keine Einstellungsdatei vorhanden, Defaults werden verwendet ({SettingsPath}).")]
    public static partial void NoSettingsFile(ILogger logger, string settingsPath);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Einstellungen aus {SettingsPath} geladen.")]
    public static partial void SettingsLoaded(ILogger logger, string settingsPath);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Einstellungen gespeichert in {SettingsPath}.")]
    public static partial void SettingsSaved(ILogger logger, string settingsPath);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Warning,
        Message = "Einstellungsdatei {SettingsPath} ist leer; Defaults werden verwendet.")]
    public static partial void SettingsFileEmpty(ILogger logger, string settingsPath);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Warning,
        Message = "Einstellungsdatei {SettingsPath} enthielt 'null'; Defaults werden verwendet.")]
    public static partial void SettingsFileJsonNull(ILogger logger, string settingsPath);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Warning,
        Message = "Einstellungsdatei {SettingsPath} ist beschaedigt; Defaults werden verwendet.")]
    public static partial void SettingsFileCorrupted(ILogger logger, Exception exception, string settingsPath);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Warning,
        Message = "Einstellungsdatei {SettingsPath} enthaelt ungueltige Werte; Defaults werden verwendet.")]
    public static partial void SettingsFileInvalidValues(ILogger logger, Exception exception, string settingsPath);

    [LoggerMessage(
        EventId = 2020,
        Level = LogLevel.Error,
        Message = "Keine Leseberechtigung fuer Einstellungsdatei {SettingsPath}.")]
    public static partial void ReadAccessDenied(ILogger logger, Exception exception, string settingsPath);

    [LoggerMessage(
        EventId = 2021,
        Level = LogLevel.Error,
        Message = "I/O-Fehler beim Lesen der Einstellungsdatei {SettingsPath}.")]
    public static partial void ReadIoError(ILogger logger, Exception exception, string settingsPath);

    [LoggerMessage(
        EventId = 2022,
        Level = LogLevel.Error,
        Message = "Keine Schreibberechtigung fuer Einstellungsdatei {SettingsPath}.")]
    public static partial void WriteAccessDenied(ILogger logger, Exception exception, string settingsPath);

    [LoggerMessage(
        EventId = 2023,
        Level = LogLevel.Error,
        Message = "I/O-Fehler beim Schreiben der Einstellungsdatei {SettingsPath}.")]
    public static partial void WriteIoError(ILogger logger, Exception exception, string settingsPath);
}
