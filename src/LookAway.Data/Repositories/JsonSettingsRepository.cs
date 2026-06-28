using System.Text.Json;
using System.Text.Json.Serialization;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Repositories;

/// <summary>
/// Persistiert die Benutzerkonfiguration als JSON-Datei pro Windows-Benutzer.
/// </summary>
/// <remarks>
/// Schreibvorgaenge sind atomar (Schreiben in eine Temp-Datei + Rename) und
/// werden ueber einen Semaphor serialisiert, damit parallele Schreiber das
/// Zielfile nicht beschaedigen. Lesezugriffe oeffnen die Datei mit
/// <see cref="FileShare.Read"/> | <see cref="FileShare.Delete"/>, damit ein
/// parallel laufender atomarer Rename nicht durch ein Reader-Handle blockiert
/// wird. So koennen Lese- und Schreibzugriffe parallel erfolgen, ohne dass
/// das Zielfile inkonsistent oder die Operation per Exception abbricht.
/// </remarks>
public sealed class JsonSettingsRepository : ISettingsRepository, IDisposable
{
    private const string SettingsFileName = "settings.json";
    private const string TempFileSuffix = ".tmp";
    private const int FileBufferSize = 4096;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;
    private readonly ILogger<JsonSettingsRepository> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Erzeugt das Repository mit dem Standardpfad
    /// <c>%APPDATA%\LookAway\settings.json</c>.
    /// </summary>
    /// <param name="logger">Logger fuer Persistenz-Vorgaenge.</param>
    public JsonSettingsRepository(ILogger<JsonSettingsRepository> logger)
        : this(GetDefaultFilePath(), logger)
    {
    }

    /// <summary>
    /// Erzeugt das Repository mit einem expliziten Dateipfad. Vorgesehen fuer
    /// Integrationstests, die ein Temp-Verzeichnis nutzen.
    /// </summary>
    /// <param name="filePath">Absoluter Pfad zur Konfigurationsdatei.</param>
    /// <param name="logger">Logger fuer Persistenz-Vorgaenge.</param>
    public JsonSettingsRepository(string filePath, ILogger<JsonSettingsRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);

        _filePath = filePath;
        _logger = logger;
    }

    /// <summary>
    /// Standardpfad der Konfigurationsdatei im Roaming-AppData des aktuellen
    /// Windows-Benutzers.
    /// </summary>
    public static string GetDefaultFilePath()
        => Path.Combine(AppDataLocation.GetDataDirectory(), SettingsFileName);

    /// <inheritdoc />
    public async Task<Settings> LoadAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        string json;
        try
        {
            FileStream stream = new(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using (stream.ConfigureAwait(false))
            {
                using StreamReader reader = new(stream);
                json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (FileNotFoundException)
        {
            JsonSettingsRepositoryLog.NoSettingsFile(_logger, _filePath);
            return CreateFirstRunDefaults();
        }
        catch (DirectoryNotFoundException)
        {
            JsonSettingsRepositoryLog.NoSettingsFile(_logger, _filePath);
            return CreateFirstRunDefaults();
        }
        catch (UnauthorizedAccessException ex)
        {
            JsonSettingsRepositoryLog.ReadAccessDenied(_logger, ex, _filePath);
            throw;
        }
        catch (IOException ex)
        {
            JsonSettingsRepositoryLog.ReadIoError(_logger, ex, _filePath);
            throw;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            JsonSettingsRepositoryLog.SettingsFileEmpty(_logger, _filePath);
            return CreateFirstRunDefaults();
        }

        try
        {
            Settings? settings = JsonSerializer.Deserialize<Settings>(json, SerializerOptions);
            if (settings is null)
            {
                JsonSettingsRepositoryLog.SettingsFileJsonNull(_logger, _filePath);
                return CreateFirstRunDefaults();
            }

            JsonSettingsRepositoryLog.SettingsLoaded(_logger, _filePath);
            return settings;
        }
        catch (JsonException ex)
        {
            JsonSettingsRepositoryLog.SettingsFileCorrupted(_logger, ex, _filePath);
            return CreateFirstRunDefaults();
        }
        catch (ArgumentException ex)
        {
            // Validierung in Settern hat einen ungueltigen Wert in der Datei erkannt.
            JsonSettingsRepositoryLog.SettingsFileInvalidValues(_logger, ex, _filePath);
            return CreateFirstRunDefaults();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ThrowIfDisposed();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            string tempPath = _filePath + TempFileSuffix;
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(settings, SerializerOptions);

            FileStream tempStream = new(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough);

            await using (tempStream.ConfigureAwait(false))
            {
                await tempStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                await tempStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await AtomicFile.ReplaceWithRetryAsync(tempPath, _filePath, cancellationToken).ConfigureAwait(false);

            JsonSettingsRepositoryLog.SettingsSaved(_logger, _filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            JsonSettingsRepositoryLog.WriteAccessDenied(_logger, ex, _filePath);
            throw;
        }
        catch (IOException ex)
        {
            JsonSettingsRepositoryLog.WriteIoError(_logger, ex, _filePath);
            throw;
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }

    /// <summary>
    /// Gibt den Schreib-Semaphor frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeLock.Dispose();
        _disposed = true;
    }

    private static Settings CreateFirstRunDefaults() => new() { IsFirstRun = true };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
