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
        CrashReport report = new(
            Timestamp: now,
            Source: source,
            ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            Message: _sanitizer.Sanitize(exception.Message),
            StackTrace: _sanitizer.Sanitize(exception.ToString()),
            ManagedThreadId: Environment.CurrentManagedThreadId,
            OsVersion: RuntimeInformationSafe.OsVersionDescription,
            RuntimeVersion: Environment.Version.ToString());

        string fileName = $"crash-{now:yyyyMMdd-HHmmss-fff}.json";
        string filePath;

        lock (_lock)
        {
            try
            {
                _ = Directory.CreateDirectory(_crashDirectory);
                filePath = Path.Combine(_crashDirectory, fileName);
                string json = JsonSerializer.Serialize(report, SerializerOptions);
                File.WriteAllText(filePath, json);

                // Beim Erzeugen eines neuen Berichts ein vorhandenes
                // Acknowledged-Marker-File entfernen, damit der nächste
                // Start den neuen Crash erkennt.
                string ackPath = Path.Combine(_crashDirectory, AcknowledgedFileName);
                if (File.Exists(ackPath))
                {
                    File.Delete(ackPath);
                }
            }
            catch (IOException)
            {
                // Persistenter Crash beim Schreiben des Crash-Reports — bewusst geschluckt.
            }
            catch (UnauthorizedAccessException)
            {
                // wie oben: bewusst geschluckt
            }
            catch (NotSupportedException)
            {
                // wie oben: bewusst geschluckt
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
                if (!Directory.Exists(_crashDirectory))
                {
                    return false;
                }

                bool anyCrashes = Directory.EnumerateFiles(_crashDirectory, "crash-*.json").Any();
                if (!anyCrashes)
                {
                    return false;
                }

                string ackPath = Path.Combine(_crashDirectory, AcknowledgedFileName);
                if (!File.Exists(ackPath))
                {
                    return true;
                }

                DateTime ackTime = File.GetLastWriteTimeUtc(ackPath);
                foreach (string file in Directory.EnumerateFiles(_crashDirectory, "crash-*.json"))
                {
                    if (File.GetLastWriteTimeUtc(file) > ackTime)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
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
            catch (IOException)
            {
                // Bestätigungsvermerk ist unkritisch — Schreibfehler bewusst geschluckt.
            }
            catch (UnauthorizedAccessException)
            {
                // wie oben: bewusst geschluckt
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
/// Kleine Indirection, damit OS-/Runtime-Infos im Test überschreibbar bleiben,
/// ohne <see cref="System.Runtime.InteropServices.RuntimeInformation"/> zu mocken.
/// </summary>
internal static class RuntimeInformationSafe
{
    /// <summary>
    /// Liefert eine kompakte Beschreibung der Betriebssystem-Version.
    /// </summary>
    public static string OsVersionDescription => Environment.OSVersion.VersionString;
}
