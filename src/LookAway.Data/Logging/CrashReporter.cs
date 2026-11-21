using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Logging;

/// <summary>
/// Schreibt unbehandelte Exceptions als JSON-Dateien in
/// <c>logs\crashes\</c>. Erkennt beim nächsten Start, ob noch ungelesene
/// Crash-Berichte existieren.
/// </summary>
/// <remarks>
/// Sämtliche Schreib-Operationen sind in Try/Catch-Blocks gekapselt:
/// Ein Folgefehler beim Schreiben des Crash-Berichts darf den ohnehin
/// laufenden Crash-Pfad nicht erneut werfen.
/// </remarks>
public sealed class CrashReporter : ICrashReporter
{
    private const string AcknowledgedFileName = ".acknowledged";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Crash-Berichte sind lokale Dateien — kein HTML-Encoding nötig.
        // So bleibt z. B. der Sanitizer-Platzhalter <user> unverändert.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _crashDirectory;
    private readonly LogMessageSanitizer _sanitizer;
    private readonly object _lock = new();

    /// <summary>
    /// Erzeugt einen CrashReporter, der in das angegebene Verzeichnis schreibt.
    /// </summary>
    /// <param name="crashDirectory">Zielverzeichnis für Crash-Dateien.</param>
    /// <param name="sanitizer">Optionaler Sanitizer; <c>null</c> erzeugt einen Default.</param>
    public CrashReporter(string crashDirectory, LogMessageSanitizer? sanitizer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(crashDirectory);

        _crashDirectory = crashDirectory;
        _sanitizer = sanitizer ?? new LogMessageSanitizer();
    }

    /// <inheritdoc />
    public void Report(Exception exception, string source)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CrashReport report = CreateReport(exception, source, now);
        string fileName = $"crash-{now:yyyyMMdd-HHmmss-fff}.json";

        lock (_lock)
        {
            try
            {
                _ = Directory.CreateDirectory(_crashDirectory);
                File.WriteAllText(Path.Combine(_crashDirectory, fileName), JsonSerializer.Serialize(report, SerializerOptions));
                RemoveAcknowledgement();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Scheitert schon der Bericht, bleibt der laufende Absturz die eigentliche
                // Nachricht; ein zweiter Fehler aus dieser Stelle würde ihn verdecken.
            }
        }
    }

    /// <inheritdoc />
    public bool HasUnresolvedCrashes()
    {
        lock (_lock)
        {
            try
            {
                return Directory.Exists(_crashDirectory) && AnyReportNewerThanAcknowledgement();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    // Ein neuer Bericht macht eine vorhandene Quittung ungültig, damit der nächste Start den neuen
    // Absturz erkennt.
    private void RemoveAcknowledgement()
    {
        string ackPath = Path.Combine(_crashDirectory, AcknowledgedFileName);
        if (File.Exists(ackPath))
        {
            File.Delete(ackPath);
        }
    }

    private CrashReport CreateReport(Exception exception, string source, DateTimeOffset now) => new(
        Timestamp: now,
        Source: source,
        ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
        Message: _sanitizer.Sanitize(exception.Message),
        StackTrace: _sanitizer.Sanitize(exception.ToString()),
        ManagedThreadId: Environment.CurrentManagedThreadId,
        OsVersion: RuntimeInformationSafe.OsVersionDescription,
        RuntimeVersion: Environment.Version.ToString());

    // Ein Bericht gilt als offen, solange ihn keine Quittung abdeckt: Fehlt die Quittung, ist jeder
    // Bericht offen; sonst nur einer, der nach ihr geschrieben wurde.
    private bool AnyReportNewerThanAcknowledgement()
    {
        string ackPath = Path.Combine(_crashDirectory, AcknowledgedFileName);
        DateTime? acknowledgedAt = File.Exists(ackPath) ? File.GetLastWriteTimeUtc(ackPath) : null;

        return Directory.EnumerateFiles(_crashDirectory, "crash-*.json")
            .Any(file => acknowledgedAt is not { } ack || File.GetLastWriteTimeUtc(file) > ack);
    }

    /// <inheritdoc />
    public void MarkResolved()
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(_crashDirectory))
                {
                    return;
                }

                string ackPath = Path.Combine(_crashDirectory, AcknowledgedFileName);
                File.WriteAllText(
                    ackPath,
                    DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Fehlt der Vermerk, meldet der nächste Start denselben Absturz noch einmal; mehr geht nicht verloren.
            }
        }
    }
}

/// <summary>
/// Datensatz für eine einzelne Crash-Datei.
/// </summary>
internal sealed record CrashReport(
    DateTimeOffset Timestamp,
    string Source,
    string ExceptionType,
    string Message,
    string StackTrace,
    int ManagedThreadId,
    string OsVersion,
    string RuntimeVersion);

/// <summary>
/// Liefert die Betriebssystem-Angabe für den Crash-Bericht. Ein fester Getter ohne
/// Schalter: Tests sehen hier dieselbe Angabe wie das laufende Programm.
/// </summary>
internal static class RuntimeInformationSafe
{
    /// <summary>
    /// Liefert eine kompakte Beschreibung der Betriebssystem-Version.
    /// </summary>
    public static string OsVersionDescription => Environment.OSVersion.VersionString;
}
