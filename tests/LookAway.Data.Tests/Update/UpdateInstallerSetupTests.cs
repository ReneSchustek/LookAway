using LookAway.Core.ValueObjects;
using LookAway.Data.Tests.Fakes;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Das Bereitstellen des Setups für eine installierte Kopie: geladen samt Signatur, geprüft an der
/// gesperrten Datei, und ohne gültige Signatur gar nicht.
/// </summary>
public sealed class UpdateInstallerSetupTests : UpdateInstallerTestBase
{
    private const string SetupUrl = "https://example.com/LookAway-Setup-v1.5.0.exe";
    private const string SetupSignatureUrl = "https://example.com/LookAway-Setup-v1.5.0.exe.sig";

    private static readonly byte[] SetupBytes = "MZ-setup-bytes"u8.ToArray();

    private string StagingRoot => Path.Combine(Root, "updates");

    [Fact]
    public async Task PrepareSetup_WithAValidSignature_ReturnsTheLockedSetup()
    {
        UpdateInstallerService service = CreateService(SetupFiles(Sign(SetupBytes)), out _);

        using VerifiedSetup? setup = await service.PrepareSetupAsync(SetupInfo(), TestContext.Current.CancellationToken);

        Assert.NotNull(setup);
        Assert.Equal(Path.Combine(StagingRoot, "1.5.0", "LookAway-Setup.exe"), setup.Path);
        Assert.Equal("1.5.0", setup.Version);
        Assert.Equal(SetupBytes, await File.ReadAllBytesAsync(setup.Path, TestContext.Current.CancellationToken));
    }

    /// <remarks>Zwischen Prüfen und Starten darf niemand die geprüfte Datei austauschen.</remarks>
    [Fact]
    public async Task PrepareSetup_KeepsTheFileLockedUntilDisposed()
    {
        UpdateInstallerService service = CreateService(SetupFiles(Sign(SetupBytes)), out _);
        VerifiedSetup? setup = await service.PrepareSetupAsync(SetupInfo(), TestContext.Current.CancellationToken);
        Assert.NotNull(setup);

        _ = Assert.Throws<IOException>(() => File.WriteAllBytes(setup.Path, [1, 2, 3]));
        _ = Assert.Throws<IOException>(() => File.Delete(setup.Path));

        setup.Dispose();
        File.WriteAllBytes(setup.Path, [1, 2, 3]);
    }

    [Fact]
    public async Task PrepareSetup_ReusesAnAlreadyDownloadedSetup()
    {
        UpdateInstallerService service = CreateService(SetupFiles(Sign(SetupBytes)), out FakeHttpGetClient http);
        using (await service.PrepareSetupAsync(SetupInfo(), TestContext.Current.CancellationToken))
        {
        }

        using VerifiedSetup? again = await service.PrepareSetupAsync(SetupInfo(), TestContext.Current.CancellationToken);

        Assert.NotNull(again);
        Assert.Equal(2, http.DownloadCount); // Setup und Signatur einmal, nicht zweimal
    }

    /// <remarks>Beide Dateien gehen weg, damit der nächste Versuch neu lädt.</remarks>
    [Fact]
    public async Task PrepareSetup_WithAWrongSignature_ReturnsNothingAndDiscardsTheFiles()
    {
        UpdateInstallerService service = CreateService(SetupFiles(Sign("andere Bytes"u8.ToArray())), out _);

        VerifiedSetup? setup = await service.PrepareSetupAsync(SetupInfo(), TestContext.Current.CancellationToken);

        Assert.Null(setup);
        Assert.Empty(Directory.GetFiles(Path.Combine(StagingRoot, "1.5.0")));
    }

    [Fact]
    public async Task PrepareSetup_WhenTheSignatureCannotBeLoaded_LeavesNoPartialFile()
    {
        Dictionary<string, byte[]> onlySetup = new(StringComparer.Ordinal) { [SetupUrl] = SetupBytes };
        UpdateInstallerService service = CreateService(onlySetup, out _);

        VerifiedSetup? setup = await service.PrepareSetupAsync(SetupInfo(), TestContext.Current.CancellationToken);

        Assert.Null(setup);
        Assert.DoesNotContain(Directory.GetFiles(Path.Combine(StagingRoot, "1.5.0")), file => file.Contains(".part-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareSetup_WithoutASignedSetupInTheRelease_ReturnsNothing()
    {
        UpdateInstallerService service = CreateService(SetupFiles(Sign(SetupBytes)), out FakeHttpGetClient http);
        UpdateInfo withoutSignature = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, new ReleaseAssets(null, null, SetupUrl));

        VerifiedSetup? setup = await service.PrepareSetupAsync(withoutSignature, TestContext.Current.CancellationToken);

        Assert.Null(setup);
        Assert.Equal(0, http.DownloadCount);
    }

    [Fact]
    public async Task PrepareSetup_WithoutInfo_Throws()
    {
        UpdateInstallerService service = CreateService(SetupFiles(Sign(SetupBytes)), out _);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => service.PrepareSetupAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void VerifyStream_ChecksTheOpenStream()
    {
        using MemoryStream valid = new(SetupBytes);
        using MemoryStream changed = new("verändert"u8.ToArray());

        Assert.True(Verifier.VerifyStream(valid, Sign(SetupBytes)));
        Assert.False(Verifier.VerifyStream(changed, Sign(SetupBytes)));
        Assert.False(Verifier.VerifyStream(new MemoryStream(SetupBytes), []));
        _ = Assert.Throws<ArgumentNullException>(() => Verifier.VerifyStream(null!, Sign(SetupBytes)));
    }

    [Fact]
    public void VerifiedSetup_RejectsMissingParts()
    {
        using MemoryStream handle = new();

        _ = Assert.Throws<ArgumentException>(() => new VerifiedSetup(" ", "1.0.0", handle));
        _ = Assert.Throws<ArgumentException>(() => new VerifiedSetup("setup.exe", "", handle));
        _ = Assert.Throws<ArgumentNullException>(() => new VerifiedSetup("setup.exe", "1.0.0", null!));
    }

    private static UpdateInfo SetupInfo()
        => UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, new ReleaseAssets(null, null, SetupUrl, SetupSignatureUrl));

    private static Dictionary<string, byte[]> SetupFiles(byte[] signature)
        => new(StringComparer.Ordinal) { [SetupUrl] = SetupBytes, [SetupSignatureUrl] = signature };

    private UpdateInstallerService CreateService(Dictionary<string, byte[]> files, out FakeHttpGetClient http)
    {
        http = new FakeHttpGetClient(files);
        return new UpdateInstallerService(http, NullLogger<UpdateInstallerService>.Instance, StagingRoot, Verifier);
    }
}
