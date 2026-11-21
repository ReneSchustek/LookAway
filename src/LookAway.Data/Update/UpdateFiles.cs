using System.Security.Cryptography;

namespace LookAway.Data.Update;

/// <summary>
/// Datei-Helfer, die Bereitstellen, Vertrauensprüfung und Datei-Tausch gemeinsam brauchen:
/// Pfad der Programmdatei, ihr Hash, die Schreibprobe und das Aufräumen, das nie wirft.
/// </summary>
internal static class UpdateFiles
{
    /// <summary>Name der Programmdatei in jedem Staging- und Programmordner.</summary>
    public const string ExecutableName = "LookAway.exe";

    /// <summary>Pfad der Programmdatei innerhalb eines Ordners.</summary>
    /// <param name="directory">Staging- oder Programmordner.</param>
    /// <returns>Der volle Pfad der Programmdatei.</returns>
    public static string ExecutablePathIn(string directory) => Path.Combine(directory, ExecutableName);

    /// <summary>Berechnet den SHA-256 (Hex, Kleinbuchstaben) einer Datei.</summary>
    /// <param name="path">Dateipfad.</param>
    /// <returns>Hex-Hash.</returns>
    public static string ComputeFileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Prüft, ob in das Verzeichnis geschrieben werden darf (Probe-Datei).</summary>
    /// <param name="directory">Zu prüfendes Verzeichnis.</param>
    /// <returns><c>true</c>, wenn beschreibbar.</returns>
    public static bool IsDirectoryWritable(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        string probe = Path.Combine(directory, ".lookaway-write-probe.tmp");
        try
        {
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        // Jeder Datei- oder Rechtefehler bedeutet schlicht: nicht beschreibbar.
        catch (Exception ex) when (IsFileAccessFailure(ex))
        {
            return false;
        }
    }

    /// <summary>Datei- oder Rechtefehler, die ein Aufräumen oder Lesen schlicht scheitern lassen.</summary>
    /// <param name="exception">Die Ausnahme.</param>
    /// <returns><c>true</c> für Ein-/Ausgabe- und Zugriffsfehler.</returns>
    public static bool IsFileAccessFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException;

    /// <summary>Löscht eine Datei, falls vorhanden; scheitert still.</summary>
    /// <param name="path">Dateipfad.</param>
    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (IsFileAccessFailure(ex))
        {
            // Aufräumen ist best-effort: eine gesperrte Datei wird beim nächsten Lauf entfernt.
        }
    }

    /// <summary>Löscht einen Ordner samt Inhalt; scheitert still.</summary>
    /// <param name="path">Ordnerpfad.</param>
    public static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (IsFileAccessFailure(ex))
        {
            // Aufräumen ist best-effort: ein gesperrter Ordner wird beim nächsten Lauf entfernt.
        }
    }
}
