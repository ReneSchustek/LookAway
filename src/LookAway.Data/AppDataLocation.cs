using LookAway.Core.Domain;

namespace LookAway.Data;

/// <summary>
/// Ermittelt das Datenverzeichnis zur Laufzeit. Portable-Modus wird
/// erkannt, wenn neben der EXE die Datei <c>portable.flag</c> liegt.
/// </summary>
public static class AppDataLocation
{
    private static string? _baseDirectoryOverride;

    /// <summary>
    /// Programmverzeichnis, aus dem der Datenort abgeleitet wird — normalerweise das
    /// eigene, für den Update-Helfer das der bedienten Installation.
    /// </summary>
    private static string BaseDirectory => _baseDirectoryOverride ?? AppContext.BaseDirectory;

    /// <summary>
    /// Leitet den Datenort von einem fremden Programmverzeichnis ab. Der Update-Helfer
    /// startet aus dem Staging-Ordner, bedient aber die Installation im Zielordner: Ohne
    /// diese Umleitung suchte er Einstellungen und Logs neben sich statt im Datenverzeichnis
    /// der Installation — und hielte sich obendrein für portabel, sobald die
    /// Portable-Markierung im Paket mit gestaged wurde.
    /// </summary>
    /// <param name="baseDirectory">Programmverzeichnis der bedienten Installation.</param>
    /// <remarks>Muss vor dem Aufbau der Dienste gesetzt werden, sonst greift es nicht mehr.</remarks>
    public static void UseBaseDirectory(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _baseDirectoryOverride = baseDirectory;
    }

    /// <summary>Wahr, wenn die Anwendung portabel betrieben wird.</summary>
    public static bool IsPortable()
        => File.Exists(Path.Combine(BaseDirectory, AppPaths.PortableFlagFileName));

    /// <summary>
    /// Liefert das aktuell gültige Datenverzeichnis (portabel neben der EXE,
    /// sonst <c>%APPDATA%\LookAway</c>).
    /// </summary>
    public static string GetDataDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return AppPaths.ResolveDataDirectory(IsPortable(), BaseDirectory, appData);
    }
}
