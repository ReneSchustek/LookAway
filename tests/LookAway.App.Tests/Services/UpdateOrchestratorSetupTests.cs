using System.ComponentModel;
using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Entities;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Der Update-Ablauf einer installierten Kopie: Sie lädt und prüft das Setup im Hintergrund vor und
/// startet es erst auf Klick; ein ZIP wird nie eingespielt.
/// </summary>
public sealed class UpdateOrchestratorSetupTests : IDisposable
{
    private const string ReleasePage = "https://github.com/ReneSchustek/LookAway/releases/tag/v9.0.0";

    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "lookaway-setup-update-tests", TestIds.Next().ToString("N"));
    private readonly FakeUpdateProcess _process;
    private readonly PackageServer _server = new();

    public UpdateOrchestratorSetupTests()
    {
        string installDirectory = Path.Combine(_root, "Programm");
        _ = Directory.CreateDirectory(installDirectory);
        _process = new FakeUpdateProcess(installDirectory) { IsSetupInstallation = true };
    }

    private string StagingRoot => Path.Combine(_root, "updates");

    private string ExpectedSetup => Path.Combine(StagingRoot, "9.0.0", "LookAway-Setup.exe");

    public void Dispose()
    {
        _server.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ManualUpdate_StartsTheVerifiedSetup()
    {
        UpdateOrchestrator orchestrator = await FoundUpdateAsync(Release(withSetup: true));

        await orchestrator.HandleManualUpdateRequestedAsync();

        Assert.Equal([ExpectedSetup], _process.SetupsStarted);
        Assert.Empty(_process.AppliesStarted);
        Assert.Empty(_process.BrowserOpened);
    }

    [Fact]
    public async Task ManualUpdate_WithoutASignedSetup_OpensTheReleasePage()
    {
        UpdateOrchestrator orchestrator = await FoundUpdateAsync(Release(withSetup: false));

        await orchestrator.HandleManualUpdateRequestedAsync();

        Assert.Empty(_process.SetupsStarted);
        Assert.Equal(ReleasePage, Assert.Single(_process.BrowserOpened).ToString());
    }

    [Fact]
    public async Task ManualUpdate_WhenTheSetupCannotStart_OpensTheReleasePage()
    {
        _process.StartSetupFailure = new Win32Exception(5);
        UpdateOrchestrator orchestrator = await FoundUpdateAsync(Release(withSetup: true));

        await orchestrator.HandleManualUpdateRequestedAsync();

        _ = Assert.Single(_process.BrowserOpened);
    }

    [Fact]
    public async Task ManualUpdate_WithoutAFoundUpdate_StartsNothing()
    {
        UpdateOrchestrator orchestrator = CreateOrchestrator(new InMemorySettingsRepository(new Settings()), new FakeUpdateChecker());

        await orchestrator.HandleManualUpdateRequestedAsync();

        Assert.Empty(_process.SetupsStarted);
    }

    /// <remarks>
    /// Im Hintergrund nur laden und prüfen: Die Installation fragt nach Administratorrechten und
    /// darf deshalb nicht ungefragt beim Start erscheinen.
    /// </remarks>
    [Fact]
    public async Task AutoUpdate_PreparesTheSetupWithoutStartingItOrStagingThePackage()
    {
        InMemorySettingsRepository repository = new(new Settings { AutoUpdate = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new FakeUpdateChecker(Release(withSetup: true)));

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(ExpectedSetup));
        Assert.Empty(_process.SetupsStarted);
        Assert.False(File.Exists(Path.Combine(StagingRoot, "9.0.0", "LookAway.exe")));
        Assert.Null((await repository.LoadAsync(TestContext.Current.CancellationToken)).PendingUpdateVersion);
    }

    private async Task<UpdateOrchestrator> FoundUpdateAsync(UpdateInfo release)
    {
        InMemorySettingsRepository repository = new(new Settings { AutoUpdate = false });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new FakeUpdateChecker(release));
        Settings settings = await repository.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        await orchestrator.CheckAtStartupAsync(settings, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return orchestrator;
    }

    private static UpdateInfo Release(bool withSetup)
        => UpdateInfo.Create(
            new Version(1, 2, 7),
            "v9.0.0",
            ReleasePage,
            null,
            new ReleaseAssets(
                PackageServer.PackageAddress,
                PackageServer.SignatureAddress,
                withSetup ? PackageServer.SetupAddress : null,
                withSetup ? PackageServer.SetupSignatureAddress : null));

    private UpdateOrchestrator CreateOrchestrator(InMemorySettingsRepository repository, FakeUpdateChecker checker)
        => new(
            new UpdateInstallerService(_server, NullLogger<UpdateInstallerService>.Instance, StagingRoot, _server.Verifier, copyRetryDelay: TimeSpan.Zero),
            checker,
            repository,
            new FakeClock(Now),
            _process,
            new Version(1, 2, 7),
            NullLogger<UpdateOrchestrator>.Instance);
}
