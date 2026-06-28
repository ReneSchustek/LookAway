namespace LookAway.Core.Domain;

/// <summary>
/// Bestimmt das Datenverzeichnis der Anwendung. Im Portable-Modus
/// liegen Konfiguration, Historie und Logs neben der EXE, sonst unter
/// <c>%APPDATA%\LookAway</c>. Reine Logik — ohne Dateisystem testbar.
/// </summary>
public static class AppPaths
{
    /// <summary>Name des Anwendungsordners im Roaming-AppData.</summary>
    public const string AppFolderName = "LookAway";

    /// <summary>Dateiname der Portable-Markierung neben der EXE.</summary>
    public const string PortableFlagFileName = "portable.flag";

    /// <summary>
    /// Liefert das Datenverzeichnis.
    /// </summary>
    /// <param name="isPortable">Wird die Anwendung portabel betrieben?</param>
    /// <param name="baseDirectory">Verzeichnis der EXE.</param>
    /// <param name="appDataDirectory">Roaming-AppData-Verzeichnis des Benutzers.</param>
    /// <returns>
    /// Im Portable-Modus <paramref name="baseDirectory"/>, sonst
    /// <c>&lt;appData&gt;\LookAway</c>.
    /// </returns>
    public static string ResolveDataDirectory(bool isPortable, string baseDirectory, string appDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);

        return isPortable
            ? baseDirectory
            : Path.Combine(appDataDirectory, AppFolderName);
    }
}
