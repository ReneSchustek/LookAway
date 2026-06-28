using LookAway.Core.Domain;

namespace LookAway.Data;

/// <summary>
/// Ermittelt das Datenverzeichnis zur Laufzeit. Portable-Modus wird
/// erkannt, wenn neben der EXE die Datei <c>portable.flag</c> liegt.
/// </summary>
public static class AppDataLocation
{
    /// <summary>Wahr, wenn die Anwendung portabel betrieben wird.</summary>
    public static bool IsPortable()
        => File.Exists(Path.Combine(AppContext.BaseDirectory, AppPaths.PortableFlagFileName));

    /// <summary>
    /// Liefert das aktuell gueltige Datenverzeichnis (portabel neben der EXE,
    /// sonst <c>%APPDATA%\LookAway</c>).
    /// </summary>
    public static string GetDataDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return AppPaths.ResolveDataDirectory(IsPortable(), AppContext.BaseDirectory, appData);
    }
}
