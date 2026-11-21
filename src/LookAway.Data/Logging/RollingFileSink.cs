using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Logging;

/// <summary>
/// Schreibt strukturierte Log-Einträge in eine tagesbasierte Datei und
/// räumt ältere Dateien gemäß Retention auf.
/// </summary>
/// <remarks>
/// Schreibvorgänge sind durch ein Lock serialisiert. IO-Fehler werden
/// geschluckt — Logging darf die Anwendung nicht zum Absturz bringen.
/// Das Aufräumen alter Dateien läuft maximal einmal pro Tag, getriggert
/// durch den ersten Schreibvorgang am neuen Tag.
/// </remarks>
public sealed class RollingFileSink : IDisposable
{
    private const string LogFilePrefix = "lookaway-";
    private const string LogFileSuffix = ".log";
    private const string LogFileSearchPattern = LogFilePrefix + "*" + LogFileSuffix;

    private readonly string _directory;
    private readonly int _retentionDays;
    private readonly LogMessageSanitizer _sanitizer;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly string _runTag;
    private readonly object _lock = new();
    private DateOnly _lastRetentionRunOn = DateOnly.MinValue;
    private bool _disposed;

    /// <summary>
    /// Erzeugt einen Sink, der in <paramref name="directory"/> schreibt.
    /// </summary>
    /// <param name="directory">Zielverzeichnis für die Log-Dateien.</param>
    /// <param name="retentionDays">
    /// Wie viele Tage Historie behalten werden. Die Vorgabe von sieben Tagen lässt einen
    /// Fehler vom Wochenanfang am Freitag noch nachlesen; ältere Protokolle sucht niemand.
    /// </param>
    /// <param name="sanitizer">Sanitizer für Benutzerpfade. Wenn <c>null</c>, wird ein Default angelegt.</param>
    /// <param name="utcNow">
    /// Uhr für das Aufräumen; <c>null</c> nimmt die Systemuhr. Tests geben eine feste Zeit vor,
    /// damit „zehn Tage alt“ nicht davon abhängt, wann der Test läuft.
    /// </param>
    /// <param name="runId">
    /// Kennung dieses Laufs in jeder Zeile; <c>null</c> nimmt die Prozess-ID. Sie hält die
    /// Zeilen auseinander, die alte Instanz, Update-Helfer und neue Instanz in dieselbe
    /// Tagesdatei schreiben, und verbindet die Schritte eines Updates über die Prozesse hinweg.
    /// </param>
    public RollingFileSink(
        string directory,
        int retentionDays = 7,
        LogMessageSanitizer? sanitizer = null,
        Func<DateTimeOffset>? utcNow = null,
        string? runId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 1);

        _directory = directory;
        _retentionDays = retentionDays;
        _sanitizer = sanitizer ?? new LogMessageSanitizer();
        _utcNow = utcNow ?? (static () => DateTimeOffset.UtcNow);
        _runTag = "[p" + (runId ?? Environment.ProcessId.ToString(CultureInfo.InvariantCulture)) + "] ";
    }

    /// <summary>
    /// Schreibt einen einzelnen Log-Eintrag in die heutige Log-Datei.
    /// </summary>
    /// <param name="timestamp">Zeitstempel des Eintrags.</param>
    /// <param name="level">Log-Level.</param>
    /// <param name="category">Kategorie (in der Regel der voll qualifizierte Typname).</param>
    /// <param name="message">Bereits formatierte Log-Nachricht.</param>
    /// <param name="exception">Optionale Exception, deren <c>ToString()</c> mitgeschrieben wird.</param>
    public void Write(
        DateTimeOffset timestamp,
        LogLevel level,
        string category,
        string message,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(message);

        DateOnly today = DateOnly.FromDateTime(timestamp.UtcDateTime);
        string filePath = Path.Combine(_directory, $"{LogFilePrefix}{today:yyyy-MM-dd}{LogFileSuffix}");
        string line = FormatLine(timestamp, level, category, message, exception);

        lock (_lock)
        {
            if (!_disposed)
            {
                AppendUnderLock(today, filePath, line);
            }
        }
    }

    // Hängt die Zeile an und räumt beim ersten Eintrag eines Tages alte Dateien ab. Aufrufer hält
    // die Sperre.
    private void AppendUnderLock(DateOnly today, string filePath, string line)
    {
        try
        {
            _ = Directory.CreateDirectory(_directory);

            if (today != _lastRetentionRunOn)
            {
                PruneOldFiles(today);
                _lastRetentionRunOn = today;
            }

            File.AppendAllText(filePath, line, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein Protokoll, das nicht schreiben kann, darf die Anwendung nicht mitreißen.
        }
    }

    /// <summary>
    /// Erzwingt einen Retention-Lauf unabhängig vom Tageswechsel.
    /// Vorgesehen für Tests und manuelle Aufräumvorgänge.
    /// </summary>
    public void PruneNow()
    {
        DateOnly today = DateOnly.FromDateTime(_utcNow().UtcDateTime);
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (Directory.Exists(_directory))
                {
                    PruneOldFiles(today);
                    _lastRetentionRunOn = today;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Aufräumen ist best-effort; der nächste Tageswechsel versucht es erneut.
            }
        }
    }

    /// <summary>
    /// Markiert den Sink als geschlossen. Weitere <see cref="Write"/>-Aufrufe sind No-Ops.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }
    }

    private string FormatLine(
        DateTimeOffset timestamp,
        LogLevel level,
        string category,
        string message,
        Exception? exception)
    {
        StringBuilder sb = new(capacity: 256);
        _ = sb.Append('[')
              .Append(timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))
              .Append("] [")
              .Append(LevelToString(level))
              .Append("] ")
              .Append(_runTag)
              .Append(category)
              .Append(": ")
              .AppendLine(_sanitizer.Sanitize(message));

        if (exception is not null)
        {
            _ = sb.AppendLine(_sanitizer.Sanitize(exception.ToString()));
        }

        return sb.ToString();
    }

    // Beide Aufrufer stellen den Ordner vorher sicher; verschwindet er dazwischen, meldet das
    // Aufzählen einen Ein-/Ausgabefehler, den sie abfangen.
    private void PruneOldFiles(DateOnly today)
    {
        DateTime cutoff = today.AddDays(-_retentionDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        foreach (string filePath in Directory.EnumerateFiles(_directory, LogFileSearchPattern))
        {
            try
            {
                FileInfo info = new(filePath);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    info.Delete();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Eine Datei, die gerade gelesen wird, bleibt bis zur nächsten Runde liegen.
            }
        }
    }

    private static string LevelToString(LogLevel level) => level switch
    {
        LogLevel.Trace => "Trace",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        LogLevel.None => "None",
        _ => level.ToString(),
    };
}
