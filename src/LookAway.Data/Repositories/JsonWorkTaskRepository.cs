using System.Text.Json;
using System.Text.Json.Serialization;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Repositories;

/// <summary>
/// Persistiert die Aufgaben als JSON-Array pro Windows-Benutzer.
/// </summary>
/// <remarks>
/// Anlegen, Ändern und Löschen schreiben die gesamte Liste atomar zurück. Die
/// gemeinsamen Datei-Primitive liefert <see cref="JsonFileStore"/>; Lesen und
/// Schreiben eines Vorgangs laufen unter dessen Schreib-Semaphor, damit zwei
/// gleichzeitige Änderungen sich nicht gegenseitig überschreiben.
/// </remarks>
public sealed class JsonWorkTaskRepository : IWorkTaskRepository, IDisposable
{
    private const string TasksFileName = "tasks.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly JsonFileStore _store;
    private readonly ILogger<JsonWorkTaskRepository> _logger;
    private bool _disposed;

    /// <summary>
    /// Erzeugt das Repository mit dem Standardpfad <c>%APPDATA%\LookAway\tasks.json</c>.
    /// </summary>
    /// <param name="logger">Logger für Persistenz-Vorgänge.</param>
    public JsonWorkTaskRepository(ILogger<JsonWorkTaskRepository> logger)
        : this(GetDefaultFilePath(), logger)
    {
    }

    /// <summary>
    /// Erzeugt das Repository mit einem expliziten Dateipfad (für Tests).
    /// </summary>
    /// <param name="filePath">Absoluter Pfad zur Aufgaben-Datei.</param>
    /// <param name="logger">Logger für Persistenz-Vorgänge.</param>
    public JsonWorkTaskRepository(string filePath, ILogger<JsonWorkTaskRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);

        _store = new JsonFileStore(filePath);
        _logger = logger;
    }

    /// <summary>Standardpfad der Aufgaben-Datei im Roaming-AppData.</summary>
    public static string GetDefaultFilePath()
        => Path.Combine(AppDataLocation.GetDataDirectory(), TasksFileName);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkTask>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ThrowIfDisposed();

        await _store.RunExclusiveAsync(
            async ct =>
            {
                List<WorkTask> tasks = await ReadUnlockedAsync(ct).ConfigureAwait(false);

                // Dieselbe Kennung ersetzt den vorhandenen Eintrag an Ort und Stelle:
                // Eine geänderte Aufgabe soll in der Liste nicht nach unten wandern.
                int index = tasks.FindIndex(existing => existing.Id == task.Id);
                if (index >= 0)
                {
                    tasks[index] = task;
                }
                else
                {
                    tasks.Add(task);
                }

                await WriteUnlockedAsync(tasks, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        JsonWorkTaskRepositoryLog.TaskSaved(_logger, _store.FilePath);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return await _store.RunExclusiveAsync(
            async ct =>
            {
                List<WorkTask> tasks = await ReadUnlockedAsync(ct).ConfigureAwait(false);
                int removed = tasks.RemoveAll(task => task.Id == id);

                if (removed == 0)
                {
                    return false;
                }

                await WriteUnlockedAsync(tasks, ct).ConfigureAwait(false);
                JsonWorkTaskRepositoryLog.TaskDeleted(_logger, _store.FilePath);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<WorkTask>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        string? json;
        try
        {
            json = await _store.ReadAllTextOrNullAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Aufgaben sind nicht startkritisch — bei Zugriffsfehler leer behandeln.
            JsonWorkTaskRepositoryLog.TasksReadFailed(_logger, ex, _store.FilePath);
            return [];
        }
        catch (IOException ex)
        {
            JsonWorkTaskRepositoryLog.TasksReadFailed(_logger, ex, _store.FilePath);
            return [];
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkTask>>(json, SerializerOptions) ?? [];
        }
        catch (JsonException ex)
        {
            JsonWorkTaskRepositoryLog.TasksCorrupted(_logger, ex, _store.FilePath);
            await BackUpCorruptAsync(json, cancellationToken).ConfigureAwait(false);
            return [];
        }
        catch (ArgumentException ex)
        {
            // Eine Aufgabe mit unbrauchbaren Werten lässt den Konstruktor werfen.
            JsonWorkTaskRepositoryLog.TasksCorrupted(_logger, ex, _store.FilePath);
            await BackUpCorruptAsync(json, cancellationToken).ConfigureAwait(false);
            return [];
        }
    }

    private async Task WriteUnlockedAsync(List<WorkTask> tasks, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(tasks, SerializerOptions);
        await _store.WriteBytesAtomicAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task BackUpCorruptAsync(string content, CancellationToken cancellationToken)
    {
        if (await _store.TryWriteCorruptBackupAsync(content, cancellationToken).ConfigureAwait(false))
        {
            JsonWorkTaskRepositoryLog.TasksBackedUp(_logger, _store.CorruptBackupPath);
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
/// Source-generierte Logging-Methoden des Aufgaben-Repositorys.
/// </summary>
internal static partial class JsonWorkTaskRepositoryLog
{
    [LoggerMessage(EventId = 1420, Level = LogLevel.Debug, Message = "Aufgabe in {Path} gespeichert.")]
    public static partial void TaskSaved(ILogger logger, string path);

    [LoggerMessage(EventId = 1421, Level = LogLevel.Debug, Message = "Aufgabe aus {Path} entfernt.")]
    public static partial void TaskDeleted(ILogger logger, string path);

    [LoggerMessage(EventId = 1422, Level = LogLevel.Warning, Message = "Aufgabenliste {Path} ist beschädigt — sie wird leer behandelt.")]
    public static partial void TasksCorrupted(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 1423, Level = LogLevel.Warning, Message = "Aufgabenliste {Path} konnte nicht gelesen werden — sie wird leer behandelt.")]
    public static partial void TasksReadFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 1424, Level = LogLevel.Information, Message = "Beschädigte Aufgabenliste unter {BackupPath} gesichert.")]
    public static partial void TasksBackedUp(ILogger logger, string backupPath);
}
