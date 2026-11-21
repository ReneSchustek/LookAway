using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Tests.Fakes;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Bereitstellen eines Updates: Download, Echtheitsprüfung der losgelösten Signatur, Entpacken
/// in den versionsbenannten Staging-Ordner und Aufräumen nach jedem Fehlschlag.
/// </summary>
public sealed class UpdateInstallerStagingTests : UpdateInstallerTestBase
{
    [Fact]
    public async Task DownloadAndStage_ExtractsASignedPackageIntoTheVersionFolder()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, BuildPackageZip());

        StagedUpdate? staged = await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken);

        Assert.NotNull(staged);
        Assert.True(File.Exists(Path.Combine(staged!.Directory, "LookAway.exe")));
        Assert.True(File.Exists(Path.Combine(staged.Directory, "Assets", "sound.wav")));
        Assert.EndsWith("1.5.0", staged.Directory, StringComparison.Ordinal);
        Assert.Equal("1.5.0", staged.Version);
        Assert.False(string.IsNullOrEmpty(staged.ExecutableSha256));
    }

    [Fact]
    public async Task DownloadAndStage_DoesNotCarryOverThePortableMarker()
    {
        // Der Helfer startet aus dem Staging-Ordner. Läge dort die Portable-Markierung,
        // hielte er sich für eine portable Installation, läse die Einstellungen neben sich
        // statt aus dem Datenverzeichnis der laufenden Installation — und lehnte sein
        // eigenes Update ab, weil er den vermerkten Datei-Hash dort nicht findet.
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, BuildPackageZip());

        StagedUpdate? staged = await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken);

        Assert.NotNull(staged);
        Assert.False(File.Exists(Path.Combine(staged!.Directory, "portable.flag")));
        Assert.True(File.Exists(Path.Combine(staged.Directory, "LookAway.exe")));
    }

    [Fact]
    public async Task DownloadAndStage_WithoutPackageAddress_ReturnsNull()
    {
        UpdateInstallerService service = CreateService(Path.Combine(Root, "staging"), BuildPackageZip());
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", "https://example.com/r", null);

        StagedUpdate? staged = await service.DownloadAndStageAsync(info, TestContext.Current.CancellationToken);

        Assert.Null(staged);
    }

    [Fact]
    public async Task DownloadAndStage_WithoutSignatureAddress_ReturnsNull()
    {
        // Paket vorhanden, aber keine Signatur-URL: fail-closed -> kein Update.
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, BuildPackageZip());
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, new ReleaseAssets(PackageUrl, null));

        Assert.Null(await service.DownloadAndStageAsync(info, TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.5.0")));
    }

    [Fact]
    public async Task DownloadAndStage_RejectsAnInvalidSignature()
    {
        // Signatur passt nicht zum Paket (über andere Bytes erzeugt) -> Abweisung.
        string stagingRoot = Path.Combine(Root, "staging");
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            [PackageUrl] = BuildPackageZip(),
            [SignatureUrl] = Sign(new byte[] { 1, 2, 3, 4 }),
        };
        UpdateInstallerService service = new(
            new FakeHttpGetClient(files),
            NullLogger<UpdateInstallerService>.Instance,
            stagingRoot,
            Verifier);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.5.0")));
    }

    [Fact]
    public async Task DownloadAndStage_OnDownloadFailure_ReturnsNull()
    {
        // FakeHttpGetClient ohne Inhalt -> DownloadFileAsync meldet Misserfolg.
        UpdateInstallerService service = CreateService(Path.Combine(Root, "staging"), package: null);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAndStage_WithAPackageWithoutExecutable_ReturnsNullAndCleansUp()
    {
        byte[] zipWithoutExe = BuildZip(("readme.txt", "hallo"));
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, zipWithoutExe);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.5.0")));
    }

    [Fact]
    public async Task DownloadAndStage_RejectsZipSlipAndStaysInsideTheFolder()
    {
        byte[] zipSlip = BuildZip(("../escape.exe", "böse"), ("LookAway.exe", "x"));
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, zipSlip);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken));
        // Kein Ausbruch aus dem Staging-Wurzelverzeichnis.
        Assert.False(File.Exists(Path.Combine(Root, "escape.exe")));
    }

    [Fact]
    public void Constructor_WithoutStagingRoot_UsesTheUpdatesFolderInLocalAppData()
    {
        UpdateInstallerService service = new(new FakeHttpGetClient(), NullLogger<UpdateInstallerService>.Instance);

        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppPaths.AppFolderName,
            "updates");
        Assert.Equal(expected, service.StagingRoot);
    }

    [Fact]
    public async Task DownloadAndStage_ReplacesAnEarlierStagingOfTheSameVersion()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        string earlier = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(earlier);
        string leftover = Path.Combine(earlier, "alt.txt");
        await File.WriteAllTextAsync(leftover, "rest", TestContext.Current.CancellationToken);
        UpdateInstallerService service = CreateSigningService(stagingRoot, BuildPackageZip());

        StagedUpdate? staged = await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken);

        Assert.NotNull(staged);
        Assert.False(File.Exists(leftover));
        Assert.True(File.Exists(Path.Combine(staged.Directory, "LookAway.exe")));
    }

    /// <remarks>
    /// Bleibt die Teildatei eines gescheiterten Downloads gesperrt, etwa vom Virenscanner, gibt
    /// das Aufräumen nach; der Download gilt trotzdem als gescheitert.
    /// </remarks>
    [Fact]
    public async Task DownloadAndStage_WhenTheFailedDownloadStaysLocked_ReturnsNull()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        using LockingDownloadClient client = new();
        UpdateInstallerService service = new(client, NullLogger<UpdateInstallerService>.Instance, stagingRoot, Verifier);

        StagedUpdate? staged = await service.DownloadAndStageAsync(SignedInfo(), TestContext.Current.CancellationToken);

        Assert.Null(staged);
        Assert.True(File.Exists(client.LockedPath));
    }
}
