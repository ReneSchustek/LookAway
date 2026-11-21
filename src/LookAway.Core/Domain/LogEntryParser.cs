using System.Globalization;
using System.Text;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Core.Domain;

/// <summary>
/// Liest die Zeilen einer Protokolldatei in <see cref="LogEntry"/>-Einträge.
/// </summary>
/// <remarks>
/// Gegenstück zum Schreibformat des Datei-Protokolls:
/// <c>[2026-08-11T12:34:56.789Z] [Information] [p4711] Kategorie: Meldung</c>. Die
/// Laufkennung in der dritten Klammer fehlt in älteren Dateien; deren Zeilen bleiben
/// lesbar, und ihr Wert wird nicht in den Eintrag übernommen. Zeilen ohne
/// diesen Kopf sind Fortsetzungen — vor allem die Zeilen eines Ausnahme-Stapels — und
/// gehören zum Eintrag davor. Wer sie verwirft, verliert genau den Teil, wegen dem
/// man ins Protokoll schaut; wer sie als eigene Einträge führt, bekommt eine Liste
/// aus Bruchstücken ohne Zeit und Stufe.
/// </remarks>
public static class LogEntryParser
{
    /// <summary>
    /// Parst Protokollzeilen in der gelesenen Reihenfolge.
    /// </summary>
    /// <param name="lines">Zeilen einer Protokolldatei.</param>
    /// <returns>Die erkannten Einträge; Bruchstücke vor dem ersten Kopf entfallen.</returns>
    public static IReadOnlyList<LogEntry> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<LogEntry> entries = [];
        LogEntryDraft? current = null;

        foreach (string line in lines)
        {
            if (TryParseHeader(line, out LogEntryDraft? header))
            {
                Append(entries, current);
                current = header;
                continue;
            }

            // Fortsetzung: nur sinnvoll, solange ein Eintrag offen ist. Bruchstücke am
            // Dateianfang (etwa nach einer abgeschnittenen Datei) haben keinen Kopf und
            // damit weder Zeit noch Stufe — sie werden übergangen.
            _ = current?.Continuation.AppendLine(line);
        }

        Append(entries, current);
        return entries;
    }

    private static void Append(List<LogEntry> entries, LogEntryDraft? draft)
    {
        if (draft is null)
        {
            return;
        }

        string message = draft.Continuation.Length == 0
            ? draft.Message
            : draft.Message + Environment.NewLine + draft.Continuation.ToString().TrimEnd();

        entries.Add(new LogEntry(draft.Timestamp, draft.Level, draft.Category, message));
    }

    private static bool TryParseHeader(string line, out LogEntryDraft? draft)
    {
        draft = null;

        if (string.IsNullOrEmpty(line) || line[0] != '[')
        {
            return false;
        }

        int timestampEnd = line.IndexOf(']', StringComparison.Ordinal);
        if (timestampEnd < 0 || !TryReadTimestamp(line.AsSpan(1, timestampEnd - 1), out DateTimeOffset timestamp))
        {
            return false;
        }

        if (!TryReadLevel(line, timestampEnd, out LogLevel level, out int levelEnd))
        {
            return false;
        }

        (string category, string message) = SplitCategory(SkipRunTag(line[(levelEnd + 1)..].TrimStart()));
        draft = new LogEntryDraft(timestamp, level, category, message);
        return true;
    }

    // Die Stufe steht als zweites Klammerpaar hinter dem Zeitstempel.
    private static bool TryReadLevel(string line, int timestampEnd, out LogLevel level, out int levelEnd)
    {
        level = default;
        levelEnd = -1;
        int levelStart = line.IndexOf('[', timestampEnd);
        if (levelStart < 0)
        {
            return false;
        }

        levelEnd = line.IndexOf(']', levelStart);
        return levelEnd >= 0 && Enum.TryParse(line[(levelStart + 1)..levelEnd], out level);
    }

    // Hinter der Stufe steht die Laufkennung, dann "Kategorie: Meldung". Der erste
    // Doppelpunkt trennt; spätere gehören zur Meldung.
    private static (string Category, string Message) SplitCategory(string remainder)
    {
        int separator = remainder.IndexOf(':', StringComparison.Ordinal);
        return separator < 0
            ? (string.Empty, remainder)
            : (remainder[..separator], remainder[(separator + 1)..].TrimStart());
    }

    // Die Laufkennung steht zur Unterscheidung der Prozesse in der Datei; die Protokollansicht
    // braucht sie nicht. Eine Kategorie beginnt nie mit '[', die Verwechslung ist ausgeschlossen.
    private static string SkipRunTag(string remainder)
    {
        if (!remainder.StartsWith('['))
        {
            return remainder;
        }

        int end = remainder.IndexOf(']', StringComparison.Ordinal);
        return end < 0 ? remainder : remainder[(end + 1)..].TrimStart();
    }

    private static bool TryReadTimestamp(ReadOnlySpan<char> value, out DateTimeOffset timestamp)
        => DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out timestamp);

    // Ein Eintrag im Aufbau: Die Kopfzeile ist gelesen, Fortsetzungszeilen sammeln sich,
    // bis der nächste Kopf den Eintrag abschließt.
    private sealed class LogEntryDraft(DateTimeOffset timestamp, LogLevel level, string category, string message)
    {
        public DateTimeOffset Timestamp { get; } = timestamp;

        public LogLevel Level { get; } = level;

        public string Category { get; } = category;

        public string Message { get; } = message;

        public StringBuilder Continuation { get; } = new();
    }
}
