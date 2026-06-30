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
/// Append-only: jede neue Sitzung wird der bestehenden Liste hinzugefügt und das
/// gesamte Array atomar zurückgeschrieben. Die gemeinsamen Datei-Primitive liefert
/// <see cref="JsonFileStore"/>; Lese-/Schreibvorgänge eines Append-Zyklus laufen
/// unter dessen Schreib-Semaphor.
/// </remarks>
public sealed class JsonBreakHistoryRepository : IBreakHistoryRepository, IDisposable
{
    private const string HistoryFileName = "history.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly JsonFileStore _store;
    private readonly ILogger<JsonBreakHistoryRepository> _logger;
    private bool _disposed;

    /// <summary>
    /// Erzeugt das Repository mit dem Standardpfad
    /// <c>%APPDATA%\LookAway\history.json</c>.
    /// </summary>
    /// <param name="logger">Logger für Persistenz-Vorgänge.</param>
    public JsonBreakHistoryRepository(ILogger<JsonBreakHistoryRepository> logger)
        : this(GetDefaultFilePath(), logger)
    {
    }

    /// <summary>
    /// Erzeugt das Repository mit einem expliziten Dateipfad (für Tests).
    /// </summary>
    /// <param name="filePath">Absoluter Pfad zur Historie-Datei.</param>
    /// <param name="logger">Logger für Persistenz-Vorgänge.</param>
    public JsonBreakHistoryRepository(string filePath, ILogger<JsonBreakHistoryRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);

        _store = new JsonFileStore(filePath);
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

        await _store.RunExclusiveAsync(
            async ct =>
            {
                List<BreakSession> sessions = await ReadUnlockedAsync(ct).ConfigureAwait(false);
                sessions.Add(session);
                await WriteUnlockedAsync(sessions, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        JsonBreakHistoryRepositoryLog.SessionAppended(_logger, _store.FilePath);
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

        return await _store.RunExclusiveAsync(
            async ct =>
            {
                List<BreakSession> sessions = await ReadUnlockedAsync(ct).ConfigureAwait(false);
                int before = sessions.Count;
                List<BreakSession> kept = sessions.FindAll(session => session.StartedAt >= cutoff);

                if (kept.Count == before)
                {
                    return 0;
                }

                await WriteUnlockedAsync(kept, ct).ConfigureAwait(false);
                int removed = before - kept.Count;
                JsonBreakHistoryRepositoryLog.HistoryPurged(_logger, removed, _store.FilePath);
                return removed;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<BreakSession>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        string? json;
        try
        {
            json = await _store.ReadAllTextOrNullAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Historie ist nicht startkritisch — bei Zugriffsfehler leer behandeln.
            JsonBreakHistoryRepositoryLog.HistoryReadFailed(_logger, ex, _store.FilePath);
            return new List<BreakSession>();
        }
        catch (IOException ex)
        {
            JsonBreakHistoryRepositoryLog.HistoryReadFailed(_logger, ex, _store.FilePath);
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
            // Beschädigte Historie soll die App nicht blockieren — leer starten.
            JsonBreakHistoryRepositoryLog.HistoryCorrupted(_logger, ex, _store.FilePath);
            await BackUpCorruptAsync(json, cancellationToken).ConfigureAwait(false);
            return new List<BreakSession>();
        }
        catch (ArgumentException ex)
        {
            JsonBreakHistoryRepositoryLog.HistoryCorrupted(_logger, ex, _store.FilePath);
            await BackUpCorruptAsync(json, cancellationToken).ConfigureAwait(false);
            return new List<BreakSession>();
        }
    }

    private async Task WriteUnlockedAsync(List<BreakSession> sessions, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(sessions, SerializerOptions);
        await _store.WriteBytesAtomicAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task BackUpCorruptAsync(string content, CancellationToken cancellationToken)
    {
        if (await _store.TryWriteCorruptBackupAsync(content, cancellationToken).ConfigureAwait(false))
        {
            JsonBreakHistoryRepositoryLog.HistoryBackedUp(_logger, _store.CorruptBackupPath);
        }
    }

    /// <summary>Gibt die Datei-Ressourcen frei.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _store.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Source-generierte Logging-Methoden des History-Repositorys.
/// </summary>
internal static partial class JsonBreakHistoryRepositoryLog
{
    [LoggerMessage(EventId = 1400, Level = LogLevel.Debug, Message = "Pausen-Sitzung an {Path} angehängt.")]
    public static partial void SessionAppended(ILogger logger, string path);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information, Message = "{Count} alte Historie-Einträge aus {Path} entfernt.")]
    public static partial void HistoryPurged(ILogger logger, int count, string path);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Warning, Message = "Historie {Path} ist beschädigt — sie wird leer behandelt.")]
    public static partial void HistoryCorrupted(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Warning, Message = "Historie {Path} konnte nicht gelesen werden — sie wird leer behandelt.")]
    public static partial void HistoryReadFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 1404, Level = LogLevel.Information, Message = "Beschädigte Historie unter {BackupPath} gesichert.")]
    public static partial void HistoryBackedUp(ILogger logger, string backupPath);
}
