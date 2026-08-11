using System.IO.Compression;
using LookAway.Core.Domain;

namespace LookAway.Data.Update;

/// <summary>
/// Entpackt ein ZIP-Archiv unter Auflagen: begrenzte Eintragszahl und Gesamtgröße
/// als Schutz vor Zip-Bomben, und Abweisung jedes Eintrags, der das Zielverzeichnis
/// verlassen würde.
/// </summary>
/// <remarks>
/// Als eigener Typ und nicht als Methode des Installierers: Was hier steht, sind
/// Auflagen an ein Archiv aus fremder Hand — die gelten unabhängig davon, ob gerade
/// ein Update eingespielt wird. Getrennt lassen sie sich einzeln prüfen, und zwar
/// gegen genau die Archive, gegen die sie schützen sollen.
/// </remarks>
internal static class SafeArchiveExtractor
{
    // Großzügige Obergrenzen, die ein reales Portable-Paket (self-contained, einige
    // hundert MB / einige tausend Dateien) nie erreicht: 1 GiB entpackte Gesamtgröße
    // und 20 000 Einträge.
    private const long MaxExtractedBytes = 1024L * 1024 * 1024;
    private const int MaxEntries = 20_000;

    private const int CopyBufferSize = 81920;

    /// <summary>
    /// Entpackt <paramref name="zipPath"/> nach <paramref name="destinationDir"/>.
    /// </summary>
    /// <param name="zipPath">Pfad des Archivs.</param>
    /// <param name="destinationDir">Zielverzeichnis; wird angelegt, falls nötig.</param>
    /// <exception cref="InvalidDataException">
    /// Das Archiv überschreitet eine der Obergrenzen oder enthält einen Eintrag, der
    /// aus dem Zielverzeichnis herausführt.
    /// </exception>
    public static void ExtractTo(string zipPath, string destinationDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDir);

        _ = Directory.CreateDirectory(destinationDir);
        string destinationFull = Path.GetFullPath(destinationDir) + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        if (archive.Entries.Count > MaxEntries)
        {
            throw new InvalidDataException($"ZIP enthält zu viele Einträge ({archive.Entries.Count}).");
        }

        long totalWritten = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string targetPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
            if (!targetPath.StartsWith(destinationFull, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unsicherer ZIP-Eintrag (Pfadverlassen): {entry.FullName}");
            }

            // Verzeichniseintrag (endet auf '/').
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                _ = Directory.CreateDirectory(targetPath);
                continue;
            }

            // Die Portable-Markierung des Pakets nie in den Staging-Ordner übernehmen:
            // Der Helfer startet von dort und würde sich sonst für eine portable
            // Installation halten — er läse seine Einstellungen aus dem Staging-Ordner
            // statt aus dem Datenverzeichnis der laufenden Installation, fände dort den
            // vermerkten Datei-Hash nicht und lehnte das eigene Update ab.
            if (string.Equals(entry.FullName, AppPaths.PortableFlagFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            totalWritten = CopyEntry(entry, targetPath, totalWritten);
        }
    }

    /// <remarks>
    /// Gezählt werden die tatsächlich geschriebenen Bytes, nicht die im Archiv
    /// vermerkte Größe: Die steht in der Datei und lässt sich beliebig eintragen.
    /// </remarks>
    private static long CopyEntry(ZipArchiveEntry entry, string targetPath, long totalWritten)
    {
        using Stream source = entry.Open();
        using FileStream destination = File.Create(targetPath);

        byte[] buffer = new byte[CopyBufferSize];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalWritten += read;
            if (totalWritten > MaxExtractedBytes)
            {
                throw new InvalidDataException("Entpackte Gesamtgröße überschreitet das Limit.");
            }

            destination.Write(buffer, 0, read);
        }

        return totalWritten;
    }
}
