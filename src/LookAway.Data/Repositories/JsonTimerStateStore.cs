using System.Text.Json;
using System.Text.Json.Serialization;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.Data.Repositories;

/// <summary>
/// Persistiert die <see cref="TimerSnapshot"/>-Momentaufnahme als kleine JSON-Datei
/// neben den übrigen Benutzerdaten. Bewusst schlank und fehlertolerant gehalten:
/// die Momentaufnahme ist unkritisch, daher führt jeder Lese-/Schreibfehler
/// lediglich dazu, dass der Timer regulär (auf volle Arbeitsdauer) startet.
/// </summary>
public sealed class JsonTimerStateStore : ITimerStateStore
{
    private const string FileName = "timer-state.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    /// <summary>Erzeugt den Speicher mit dem Standardpfad im Benutzerdaten-Verzeichnis.</summary>
    public JsonTimerStateStore()
        : this(Path.Combine(AppDataLocation.GetDataDirectory(), FileName))
    {
    }

    /// <summary>Erzeugt den Speicher mit einem expliziten Dateipfad (für Tests).</summary>
    /// <param name="filePath">Absoluter Pfad zur Momentaufnahme-Datei.</param>
    public JsonTimerStateStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public void Save(TimerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(snapshot, SerializerOptions));
        }
        catch (IOException)
        {
            // Momentaufnahme ist unkritisch — bei Schreibfehler einfach nicht sichern.
        }
        catch (UnauthorizedAccessException)
        {
            // wie oben: bewusst ignoriert
        }
    }

    /// <inheritdoc />
    public TimerSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            string json = File.ReadAllText(_filePath);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<TimerSnapshot>(json, SerializerOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (IOException)
        {
            // Aufräumen ist best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Aufräumen ist best-effort.
        }
    }
}
