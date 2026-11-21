using LookAway.App.Services;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Setup-Aktualisierung ohne Download und Prozess: standardmäßig eine portable Kopie, auf Wunsch
/// eine Installation mit vorgegebenem Ergebnis.
/// </summary>
internal sealed class FakeSetupUpdate : ISetupUpdate
{
    public bool IsActive { get; set; }

    /// <summary>Was <see cref="StartAsync"/> meldet.</summary>
    public bool StartSucceeds { get; set; } = true;

    public List<UpdateInfo> Started { get; } = [];

    public bool CanInstall(UpdateInfo info)
        => IsActive ? info.SetupUrl is not null && info.SetupSignatureUrl is not null : info.PackageUrl is not null;

    public Task<bool> PrepareAsync(UpdateInfo info, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> StartAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        Started.Add(info);
        return Task.FromResult(StartSucceeds);
    }
}
