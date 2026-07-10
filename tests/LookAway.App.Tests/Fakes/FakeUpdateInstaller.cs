using LookAway.Core.Services;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Test-Fake für <see cref="IUpdateInstaller"/>. Liefert ein vorgegebenes
/// Staging-Ergebnis (oder <c>null</c> für einen Fehlschlag) und zählt die Aufrufe,
/// damit die Ein-Klick-Installation des ViewModels ohne echtes Netz/Dateisystem
/// geprüft werden kann.
/// </summary>
internal sealed class FakeUpdateInstaller : IUpdateInstaller
{
    private readonly StagedUpdate? _result;

    /// <summary>Erzeugt den Fake mit dem zurückzugebenden Staging-Ergebnis.</summary>
    /// <param name="result">Ergebnis von <see cref="DownloadAndStageAsync"/>; <c>null</c> = Fehlschlag.</param>
    public FakeUpdateInstaller(StagedUpdate? result = null) => _result = result;

    /// <summary>Anzahl der Staging-Aufrufe.</summary>
    public int StageCallCount { get; private set; }

    /// <summary>Die zuletzt übergebene Aktualisierung.</summary>
    public UpdateInfo? LastInfo { get; private set; }

    /// <inheritdoc />
    public Task<StagedUpdate?> DownloadAndStageAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        StageCallCount++;
        LastInfo = info;
        return Task.FromResult(_result);
    }
}
