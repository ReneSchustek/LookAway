using System.Text.Json;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// In-Memory-Fake für <see cref="ISettingsRepository"/>. Liefert bei jedem
/// <see cref="LoadAsync"/> eine Kopie des gehaltenen Zustands (wie ein echtes
/// Repository über die Datei-Deserialisierung) und zählt Speichervorgänge.
/// </summary>
internal sealed class InMemorySettingsRepository : ISettingsRepository
{
    private Settings _settings;

    /// <summary>Erzeugt das Fake-Repository mit einem Anfangszustand.</summary>
    /// <param name="initial">Startwerte; <c>null</c> erzeugt First-Run-Defaults.</param>
    public InMemorySettingsRepository(Settings? initial = null)
    {
        _settings = Clone(initial ?? new Settings { IsFirstRun = true });
    }

    /// <summary>Anzahl der <see cref="SaveAsync"/>-Aufrufe.</summary>
    public int SaveCallCount { get; private set; }

    /// <summary>Der zuletzt persistierte Autostart-Wert.</summary>
    public bool PersistedAutoStart => _settings.AutoStart;

    /// <summary>Wird beim Sichern geworfen, wenn gesetzt (etwa eine gesperrte Datei).</summary>
    public Exception? SaveFailure { get; set; }

    /// <inheritdoc />
    public Task<Settings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Clone(_settings));
    }

    /// <inheritdoc />
    public Task SaveAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (SaveFailure is not null)
        {
            return Task.FromException(SaveFailure);
        }

        _settings = Clone(settings);
        SaveCallCount++;
        return Task.CompletedTask;
    }

    // Über JSON statt Feld für Feld, wie bei der echten Ablage: Eine neue Einstellung kommt so von
    // selbst mit, statt im Test still verloren zu gehen.
    private static Settings Clone(Settings source)
        => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(source))!; // eine serialisierte Instanz ist nie "null"
}
