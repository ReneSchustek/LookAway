using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.Data.Logging;

/// <summary>
/// Liest die tagesbasierten Protokolldateien, die <see cref="RollingFileSink"/> schreibt.
/// </summary>
/// <remarks>
/// Gelesen wird von der jüngsten Datei rückwärts, bis die gewünschte Anzahl Einträge
/// beisammen ist — nicht der ganze Bestand. Bei sieben Tagen Aufbewahrung wäre das
/// zwar verkraftbar, aber die Ansicht zeigt ohnehin nur die letzten Einträge, und
/// ein Protokoll wächst an einem schlechten Tag schnell.
/// </remarks>
public sealed class RollingFileLogReader : ILogEntryReader
{
    private const string LogFileSearchPattern = "lookaway-*.log";

    private readonly string _directory;

    /// <summary>
    /// Erzeugt einen Leser für das angegebene Protokollverzeichnis.
    /// </summary>
    /// <param name="directory">Verzeichnis mit den Protokolldateien.</param>
    public RollingFileLogReader(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> ReadRecentAsync(
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);

        List<LogEntry> collected = [];

        try
        {
            if (!Directory.Exists(_directory))
            {
                return collected;
            }

            foreach (string path in FilesNewestFirst())
            {
                cancellationToken.ThrowIfCancellationRequested();

                string[] lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<LogEntry> entries = LogEntryParser.Parse(lines);

                // Innerhalb einer Datei stehen die Einträge chronologisch; für die
                // Anzeige zählt der jüngste zuerst.
                collected.AddRange(entries.Reverse());

                if (collected.Count >= maxEntries)
                {
                    break;
                }
            }
        }
        catch (IOException)
        {
            // Das Protokoll wird nebenher geschrieben; ein belegter Zugriff ist kein
            // Fehler, den der Benutzer beheben könnte. Was gelesen wurde, wird gezeigt.
        }
        catch (UnauthorizedAccessException)
        {
            // Gleiche Begründung: fehlende Leserechte machen die Ansicht leer, nicht kaputt.
        }

        return collected.Count > maxEntries
            ? collected[..maxEntries]
            : collected;
    }

    // Der Dateiname trägt das Datum (lookaway-yyyy-MM-dd.log), deshalb sortiert die
    // absteigende Namensfolge zugleich nach Alter — ohne jede Datei anfassen zu müssen.
    private IEnumerable<string> FilesNewestFirst()
        => Directory.EnumerateFiles(_directory, LogFileSearchPattern)
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal);
}
