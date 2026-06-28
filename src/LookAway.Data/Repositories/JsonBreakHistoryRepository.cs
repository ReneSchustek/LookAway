using System.Text.Json;
using System.Text.Json.Serialization;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Repositories;

/// <summary>
/// Persistiert die Pausen-Historie als JSON-Array pro Windows-Benutzer.
/// </summary>
/// <remarks>
/// Append-only: jede neue Sitzung wird der bestehenden Liste hinzugefuegt und das
/// gesamte Array atomar (Temp-Datei + Rename) zurueckgeschrieben. Schreibvorgaenge
/// sind ueber einen Semaphor serialisiert; Lesezugriffe oeffnen die Datei mit
/// <see cref="FileShare.Delete"/>, damit ein paralleler Rename nicht blockiert.
/// </remarks>
public sealed class JsonBreakHistoryRepository : IBreakHistoryRepository, IDisposable
{
    private const string HistoryFileName = "history.json";
    private const string TempFileSuffix = ".tmp";
    private const int FileBufferSize = 4096;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly ILogger<JsonBreakHistoryRepository> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Erzeugt das Repository mit dem Standardpfad
    /// <c>%APPDATA%\LookAway\history.json</c>.
    /// </summary>
    /// <param name="logger">Logger fuer Persistenz-Vorgaenge.</param>
    public JsonBreakHistoryRepository(ILogger<JsonBreakHistoryRepository> logger)
        : this(GetDefaultFilePath(), logger)
    {
    }

    /// <summary>
    /// Erzeugt das Repository mit einem expliziten Dateipfad (fuer Tests).
    /// </summary>
    /// <param name="filePath">Absoluter Pfad zur Historie-Datei.</param>
    /// <param name="logger">Logger fuer Persistenz-Vorgaenge.</param>
    public JsonBreakHistoryRepository(string filePath, ILogger<JsonBreakHistoryRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);

        _filePath = filePath;
        _logger = logger;
    }

    /// <summary>Standardpfad der Historie-Datei im Roaming-AppData.</summary>
    public static string GetDefaultFilePath()
        => Path.Combine(AppDataLocation.GetDataDirectory(), HistoryFileName);

    /// <inheritdoc />
    public async Task AppendAsync(BreakSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ThrowIfDisposed();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<BreakSession> sessions = await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            sessions.Add(session);
            await WriteUnlockedAsync(sessions, cancellationToken).ConfigureAwait(false);
            JsonBreakHistoryRepositoryLog.SessionAppended(_logger, _filePath);
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BreakSession>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<BreakSession> sessions = await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            int before = sessions.Count;
            List<BreakSession> kept = sessions.FindAll(session => session.StartedAt >= cutoff);

            if (kept.Count == before)
            {
                return 0;
            }

            await WriteUnlockedAsync(kept, cancellationToken).ConfigureAwait(false);
            int removed = before - kept.Count;
            JsonBreakHistoryRepositoryLog.HistoryPurged(_logger, removed, _filePath);
            return removed;
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }

    private async Task<List<BreakSession>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
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
            return new List<BreakSession>();
        }
        catch (DirectoryNotFoundException)
        {
            return new List<BreakSession>();
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<BreakSession>();
        }

        try
        {
            List<BreakSession>? sessions = JsonSerializer.Deserialize<List<BreakSession>>(json, SerializerOptions);
            return sessions ?? new List<BreakSession>();
        }
        catch (JsonException ex)
        {
            // Beschaedigte Historie soll die App nicht blockieren — leer starten.
            JsonBreakHistoryRepositoryLog.HistoryCorrupted(_logger, ex, _filePath);
            return new List<BreakSession>();
        }
        catch (ArgumentException ex)
        {
            JsonBreakHistoryRepositoryLog.HistoryCorrupted(_logger, ex, _filePath);
            return new List<BreakSession>();
        }
    }

    private async Task WriteUnlockedAsync(List<BreakSession> sessions, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        string tempPath = _filePath + TempFileSuffix;
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(sessions, SerializerOptions);

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

        await MoveWithRetryAsync(tempPath, _filePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ersetzt die Zieldatei durch die Temp-Datei und wiederholt den Rename bei
    /// transienten Sharing-Fehlern (paralleler Reader, Virenscanner).
    /// </summary>
    private static async Task MoveWithRetryAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(20 * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(20 * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Gibt den Schreib-Semaphor frei.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeLock.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Source-generierte Logging-Methoden des History-Repositorys.
/// </summary>
internal static partial class JsonBreakHistoryRepositoryLog
{
    [LoggerMessage(EventId = 1400, Level = LogLevel.Debug, Message = "Pausen-Sitzung an {Path} angehaengt.")]
    public static partial void SessionAppended(ILogger logger, string path);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information, Message = "{Count} alte Historie-Eintraege aus {Path} entfernt.")]
    public static partial void HistoryPurged(ILogger logger, int count, string path);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Warning, Message = "Historie {Path} ist beschaedigt — sie wird leer behandelt.")]
    public static partial void HistoryCorrupted(ILogger logger, Exception exception, string path);
}
