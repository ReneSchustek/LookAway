using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Die Setup-Aktualisierung für sich: was sie anbietet, und dass die geprüfte Datei gesperrt
/// bleibt, bis das Setup gestartet ist.
/// </summary>
public sealed class SetupUpdateTests
{
    private static readonly UpdateInfo WithSetup = UpdateInfo.Create(
        new Version(1, 0, 0), "v2.0.0", null, null, new ReleaseAssets(null, null, "https://example.com/s.exe", "https://example.com/s.exe.sig"));

    private static readonly UpdateInfo WithPackageOnly = UpdateInfo.Create(
        new Version(1, 0, 0), "v2.0.0", null, null, new ReleaseAssets("https://example.com/p.zip", "https://example.com/p.zip.sig"));

    private readonly FakeUpdateInstaller _installer = new();
    private readonly FakeUpdateProcess _process = new(Path.GetTempPath()) { IsSetupInstallation = true };
    private readonly SetupUpdate _update;

    public SetupUpdateTests() => _update = new SetupUpdate(_installer, _process, NullLogger.Instance);

    [Fact]
    public void ForAnInstallation_OnlyASignedSetupIsInstallable()
    {
        Assert.True(_update.IsActive);
        Assert.True(_update.CanInstall(WithSetup));
        Assert.False(_update.CanInstall(WithPackageOnly));
    }

    [Fact]
    public void ForAPortableCopy_OnlyThePackageIsInstallable()
    {
        _process.IsSetupInstallation = false;

        Assert.False(_update.IsActive);
        Assert.True(_update.CanInstall(WithPackageOnly));
        Assert.False(_update.CanInstall(WithSetup));
    }

    /// <remarks>Zwischen Prüfen und Start darf niemand die Datei austauschen.</remarks>
    [Fact]
    public async Task Start_LaunchesTheSetupWhileItIsLockedAndReleasesItAfterwards()
    {
        using TrackingLock fileLock = new();
        _installer.Setup = _ => new VerifiedSetup(@"C:\Temp\LookAway-Setup.exe", "2.0.0", fileLock);
        bool lockedAtStart = false;
        _process.SetupStarting = () => lockedAtStart = !fileLock.Released;

        bool started = await _update.StartAsync(WithSetup, TestContext.Current.CancellationToken);

        Assert.True(started);
        Assert.Equal([@"C:\Temp\LookAway-Setup.exe"], _process.SetupsStarted);
        Assert.True(lockedAtStart);
        Assert.True(fileLock.Released);
    }

    [Fact]
    public async Task Start_WithoutAVerifiedSetup_StartsNothing()
    {
        bool started = await _update.StartAsync(WithSetup, TestContext.Current.CancellationToken);

        Assert.False(started);
        Assert.Empty(_process.SetupsStarted);
    }

    [Fact]
    public async Task Start_WhenWindowsRefusesTheStart_ReportsFailureAndReleasesTheFile()
    {
        using TrackingLock fileLock = new();
        _installer.Setup = _ => new VerifiedSetup(@"C:\Temp\LookAway-Setup.exe", "2.0.0", fileLock);
        _process.StartSetupFailure = new System.ComponentModel.Win32Exception(5);

        bool started = await _update.StartAsync(WithSetup, TestContext.Current.CancellationToken);

        Assert.False(started);
        Assert.True(fileLock.Released);
    }

    [Fact]
    public async Task Prepare_ReportsWhetherTheSetupIsReadyAndReleasesTheFile()
    {
        using TrackingLock fileLock = new();
        _installer.Setup = _ => new VerifiedSetup(@"C:\Temp\LookAway-Setup.exe", "2.0.0", fileLock);

        Assert.True(await _update.PrepareAsync(WithSetup, TestContext.Current.CancellationToken));
        Assert.True(fileLock.Released);
        Assert.Empty(_process.SetupsStarted);

        _installer.Setup = _ => null;
        Assert.False(await _update.PrepareAsync(WithSetup, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Calls_WithoutInfo_Throw()
    {
        _ = Assert.Throws<ArgumentNullException>(() => _update.CanInstall(null!));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => _update.PrepareAsync(null!, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => _update.StartAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_RejectsMissingParts()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new SetupUpdate(null!, _process, NullLogger.Instance));
        _ = Assert.Throws<ArgumentNullException>(() => new SetupUpdate(_installer, null!, NullLogger.Instance));
        _ = Assert.Throws<ArgumentNullException>(() => new SetupUpdate(_installer, _process, null!));
    }

    /// <summary>Steht für die Dateisperre und merkt sich, ob sie freigegeben wurde.</summary>
    private sealed class TrackingLock : IDisposable
    {
        public bool Released { get; private set; }

        public void Dispose() => Released = true;
    }
}
