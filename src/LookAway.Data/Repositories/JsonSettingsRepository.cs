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
/// Die gemeinsamen Datei-Primitive (atomares Schreiben, tolerantes Lesen,
/// Serialisierung, Sicherung beschädigter Inhalte) liefert <see cref="JsonFileStore"/>.
/// </remarks>
public sealed class JsonSettingsRepository : ISettingsRepository, IDisposable
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly JsonFileStore _store;
    private readonly ILogger<JsonSettingsRepository> _logger;
    private bool _disposed;

    /// <summary>
    /// Erzeugt das Repository mit dem Standardpfad
    /// <c>%APPDATA%\LookAway\settings.json</c>.
    /// </summary>
    /// <param name="logger">Logger für Persistenz-Vorgänge.</param>
    public JsonSettingsRepository(ILogger<JsonSettingsRepository> logger)
        : this(GetDefaultFilePath(), logger)
    {
    }

    /// <summary>
    /// Erzeugt das Repository mit einem expliziten Dateipfad. Vorgesehen für
    /// Integrationstests, die ein Temp-Verzeichnis nutzen.
    /// </summary>
    /// <param name="filePath">Absoluter Pfad zur Konfigurationsdatei.</param>
    /// <param name="logger">Logger für Persistenz-Vorgänge.</param>
    public JsonSettingsRepository(string filePath, ILogger<JsonSettingsRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);

        _store = new JsonFileStore(filePath);
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

        string? json = await ReadJsonAsync(cancellationToken).ConfigureAwait(false);

        if (json is null)
        {
            JsonSettingsRepositoryLog.NoSettingsFile(_logger, _store.FilePath);
            return CreateFirstRunDefaults();
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            JsonSettingsRepositoryLog.SettingsFileEmpty(_logger, _store.FilePath);
            return CreateFirstRunDefaults();
        }

        return await ParseOrDefaultsAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Liest die Datei, oder <c>null</c>, wenn es sie nicht gibt.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Der Inhalt der Datei, <c>null</c> bei fehlender Datei.</returns>
    /// <remarks>
    /// Lesefehler werden protokolliert und weitergereicht, nicht geschluckt: Ohne
    /// Einstellungen weiterzulaufen hieße, mit Standardwerten zu starten und sie beim
    /// nächsten Sichern über die echten zu schreiben.
    /// </remarks>
    private async Task<string?> ReadJsonAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _store.ReadAllTextOrNullAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            JsonSettingsRepositoryLog.ReadAccessDenied(_logger, ex, _store.FilePath);
            throw;
        }
        catch (IOException ex)
        {
            JsonSettingsRepositoryLog.ReadIoError(_logger, ex, _store.FilePath);
            throw;
        }
    }

    /// <summary>
    /// Wertet den Inhalt aus; bei unbrauchbarem Inhalt wird er gesichert und mit
    /// Standardwerten begonnen.
    /// </summary>
    /// <param name="json">Der gelesene Inhalt.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Die gelesenen Einstellungen oder Standardwerte.</returns>
    /// <remarks>
    /// Hier wird geschluckt statt geworfen, und das ist der Unterschied zum Lesen: Eine
    /// unlesbare Datei ist ein Zustand des Rechners, eine unbrauchbare ein Zustand der
    /// Daten. Letztere lässt sich beiseitelegen, und der Nutzer beginnt neu — statt vor
    /// einem Programm zu sitzen, das nicht mehr startet.
    /// </remarks>
    private async Task<Settings> ParseOrDefaultsAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            Settings? settings = JsonSerializer.Deserialize<Settings>(json, SerializerOptions);
            if (settings is null)
            {
                JsonSettingsRepositoryLog.SettingsFileJsonNull(_logger, _store.FilePath);
                return CreateFirstRunDefaults();
            }

            JsonSettingsRepositoryLog.SettingsLoaded(_logger, _store.FilePath);
            return settings;
        }
        catch (JsonException ex)
        {
            JsonSettingsRepositoryLog.SettingsFileCorrupted(_logger, ex, _store.FilePath);
            await BackUpCorruptAsync(json, cancellationToken).ConfigureAwait(false);
            return CreateFirstRunDefaults();
        }
        catch (ArgumentException ex)
        {
            // Validierung in Settern hat einen ungültigen Wert in der Datei erkannt.
            JsonSettingsRepositoryLog.SettingsFileInvalidValues(_logger, ex, _store.FilePath);
            await BackUpCorruptAsync(json, cancellationToken).ConfigureAwait(false);
            return CreateFirstRunDefaults();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ThrowIfDisposed();

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(settings, SerializerOptions);

        try
        {
            await _store.RunExclusiveAsync(
                ct => _store.WriteBytesAtomicAsync(payload, ct),
                cancellationToken).ConfigureAwait(false);

            JsonSettingsRepositoryLog.SettingsSaved(_logger, _store.FilePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            JsonSettingsRepositoryLog.WriteAccessDenied(_logger, ex, _store.FilePath);
            throw;
        }
        catch (IOException ex)
        {
            JsonSettingsRepositoryLog.WriteIoError(_logger, ex, _store.FilePath);
            throw;
        }
    }

    /// <summary>
    /// Gibt die Datei-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _store.Dispose();
        _disposed = true;
    }

    private static Settings CreateFirstRunDefaults() => new() { IsFirstRun = true };

    private async Task BackUpCorruptAsync(string content, CancellationToken cancellationToken)
    {
        if (await _store.TryWriteCorruptBackupAsync(content, cancellationToken).ConfigureAwait(false))
        {
            JsonSettingsRepositoryLog.SettingsBackedUp(_logger, _store.CorruptBackupPath);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
