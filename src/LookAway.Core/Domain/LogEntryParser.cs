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
/// <c>[2026-08-11T12:34:56.789Z] [Information] Kategorie: Meldung</c>. Zeilen ohne
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

        int levelStart = line.IndexOf('[', timestampEnd);
        if (levelStart < 0)
        {
            return false;
        }

        int levelEnd = line.IndexOf(']', levelStart);
        if (levelEnd < 0 || !Enum.TryParse(line[(levelStart + 1)..levelEnd], out LogLevel level))
        {
            return false;
        }

        // Hinter der Stufe steht "Kategorie: Meldung". Der erste Doppelpunkt trennt;
        // spätere gehören zur Meldung.
        string remainder = line[(levelEnd + 1)..].TrimStart();
        int separator = remainder.IndexOf(':', StringComparison.Ordinal);
        string category = separator < 0 ? string.Empty : remainder[..separator];
        string message = separator < 0 ? remainder : remainder[(separator + 1)..].TrimStart();

        draft = new LogEntryDraft(timestamp, level, category, message);
        return true;
    }

    private static bool TryReadTimestamp(ReadOnlySpan<char> value, out DateTimeOffset timestamp)
        => DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out timestamp);

    private sealed class LogEntryDraft(DateTimeOffset timestamp, LogLevel level, string category, string message)
    {
        public DateTimeOffset Timestamp { get; } = timestamp;

        public LogLevel Level { get; } = level;

        public string Category { get; } = category;

        public string Message { get; } = message;

        public StringBuilder Continuation { get; } = new();
    }
}
