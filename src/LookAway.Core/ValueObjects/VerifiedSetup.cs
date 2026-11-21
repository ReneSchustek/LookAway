namespace LookAway.Core.ValueObjects;

/// <summary>
/// Ein heruntergeladenes Setup, dessen Signatur geprüft ist. Solange diese Instanz lebt, ist die
/// Datei gegen Schreiben und Löschen gesperrt.
/// </summary>
/// <remarks>
/// Die Sperre schließt die Lücke zwischen Prüfen und Starten: Das Setup liegt im Benutzerprofil,
/// und ein anderes Programm des Benutzers könnte es sonst nach der Prüfung austauschen, bevor
/// Windows es mit Administratorrechten startet. Erst nach dem Start entsorgen.
/// </remarks>
public sealed class VerifiedSetup : IDisposable
{
    private readonly IDisposable _fileLock;

    /// <summary>Erzeugt das geprüfte Setup.</summary>
    /// <param name="path">Vollständiger Pfad der Setup-Datei.</param>
    /// <param name="version">Version, die das Setup installiert.</param>
    /// <param name="fileLock">Offener Zugriff, der die Datei gegen Schreiben sperrt; geht in den Besitz über.</param>
    public VerifiedSetup(string path, string version, IDisposable fileLock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(fileLock);
        Path = path;
        Version = version;
        _fileLock = fileLock;
    }

    /// <summary>Vollständiger Pfad der Setup-Datei.</summary>
    public string Path { get; }

    /// <summary>Version, die das Setup installiert.</summary>
    public string Version { get; }

    /// <summary>Gibt die Sperre der Datei frei.</summary>
    public void Dispose() => _fileLock.Dispose();
}
