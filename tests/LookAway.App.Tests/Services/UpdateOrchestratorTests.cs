using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Entities;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Tests für den Startpfad des <see cref="UpdateOrchestrator"/>: Ein Vermerk, zu dem kein
/// einspielbarer Staging-Ordner (mehr) gehört, wird verworfen, statt bei jedem Start erneut
/// geprüft zu werden.
/// </summary>
public sealed class UpdateOrchestratorTests : IDisposable
{
    private readonly string _stagingRoot;

    public UpdateOrchestratorTests()
    {
        _stagingRoot = Path.Combine(
            Path.GetTempPath(),
            "lookaway-orchestrator-tests",
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_stagingRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_stagingRoot))
        {
            Directory.Delete(_stagingRoot, recursive: true);
        }
    }

    private UpdateOrchestrator CreateOrchestrator(InMemorySettingsRepository repository, Version appVersion)
        => new(
            new UpdateInstallerService(
                new FakeHttpGetClient(),
                NullLogger<UpdateInstallerService>.Instance,
                _stagingRoot),
            new FakeUpdateChecker(),
            repository,
            new FakeClock(DateTimeOffset.UnixEpoch),
            appVersion,
            NullLogger<UpdateOrchestrator>.Instance);

    [Fact]
    public async Task AppliedUpdate_ClearsItsRecordOnTheNextStart()
    {
        // Nach erfolgreichem Einspielen läuft genau die vermerkte Version, ihr Staging-Ordner
        // ist abgeräumt. Bliebe der Vermerk stehen, suchte ihn jeder Start erneut.
        InMemorySettingsRepository repository = new(new Settings
        {
            PendingUpdateVersion = "1.2.7",
            PendingUpdateSha256 = "e32d5edf7f0e7f7e720fef9d03d4cc99bf2c04998abf470d5247536c0bda137e",
        });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        bool applying = await orchestrator.TryApplyPendingUpdateOnStartupAsync();

        Assert.False(applying);
        Settings persisted = await repository.LoadAsync();
        Assert.Null(persisted.PendingUpdateVersion);
        Assert.Null(persisted.PendingUpdateSha256);
    }

    [Fact]
    public async Task PendingRecord_WithoutStagingFolder_IsDiscarded()
    {
        // Neuere Version vermerkt, aber kein passender Ordner (z. B. manuell gelöscht):
        // unbrauchbar — der Vermerk darf nicht liegen bleiben.
        InMemorySettingsRepository repository = new(new Settings
        {
            PendingUpdateVersion = "9.9.9",
            PendingUpdateSha256 = "0000000000000000000000000000000000000000000000000000000000000000",
        });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.False(await orchestrator.TryApplyPendingUpdateOnStartupAsync());

        Settings persisted = await repository.LoadAsync();
        Assert.Null(persisted.PendingUpdateVersion);
        Assert.Null(persisted.PendingUpdateSha256);
    }

    [Fact]
    public async Task RegularStart_WithoutRecord_LeavesTheSettingsUntouched()
    {
        InMemorySettingsRepository repository = new(new Settings());
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.False(await orchestrator.TryApplyPendingUpdateOnStartupAsync());
        Assert.Equal(0, repository.SaveCallCount);
    }

    /// <remarks>
    /// Die Prüfung läuft beim Start im Hintergrund. Wird die Anwendung sofort wieder
    /// beendet, baut sich der Container ab, während sie noch läuft — der Vermerk des
    /// Prüfzeitpunkts träfe dann auf eine entsorgte Ablage, und zwar in einem Vorgang,
    /// den niemand mehr beobachtet. Der Abbruch beendet sie vorher.
    /// </remarks>
    [Fact]
    public async Task ShutdownDuringStartupCheck_StopsBeforeWritingAnything()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));
        Settings settings = await repository.LoadAsync();
        using CancellationTokenSource shutdown = new();
        await shutdown.CancelAsync();

        await orchestrator.CheckAtStartupAsync(settings, shutdown.Token);

        Assert.Equal(0, repository.SaveCallCount);
    }

    /// <remarks>
    /// Ohne Abbruch läuft sie durch und hält fest, wann zuletzt geprüft wurde — sonst
    /// prüfte jeder Start erneut.
    /// </remarks>
    [Fact]
    public async Task StartupCheck_RecordsTheTimeOfTheCheck()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));
        Settings settings = await repository.LoadAsync();

        await orchestrator.CheckAtStartupAsync(settings);

        Settings persisted = await repository.LoadAsync();
        _ = Assert.NotNull(persisted.LastUpdateCheck);
    }
}
