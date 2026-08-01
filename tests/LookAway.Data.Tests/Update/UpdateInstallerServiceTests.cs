using System.IO.Compression;
using System.Security.Cryptography;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;
using LookAway.Data.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Tests für den <see cref="UpdateInstallerService"/>: Download/Entpacken in den
/// Staging-Ordner inklusive Echtheitsprüfung der losgelösten Paket-Signatur,
/// Erkennung ausstehender Updates und der Datei-Tausch (inkl. Auslassen der
/// Portable-Markierung und Erhalt von Benutzerdaten).
/// </summary>
/// <remarks>
/// Da der Installer Pakete seit der Signatur-Härtung nur mit gültiger Signatur
/// einspielt (fail-closed), erzeugt die Testklasse ein eigenes ECDSA-Schlüsselpaar:
/// die Tests signieren ihre Testpakete damit und geben dem Dienst einen
/// <see cref="ReleaseSignatureVerifier"/> mit dem zugehörigen öffentlichen Schlüssel.
/// </remarks>
public sealed class UpdateInstallerServiceTests : IDisposable
{
    private const string PackageUrl = "https://example.com/p.zip";
    private const string SignatureUrl = "https://example.com/p.zip.sig";

    private readonly string _root;
    private readonly ECDsa _signingKey;
    private readonly ReleaseSignatureVerifier _verifier;

    public UpdateInstallerServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lookaway-update-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_root);

        _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _verifier = new ReleaseSignatureVerifier(Convert.ToBase64String(_signingKey.ExportSubjectPublicKeyInfo()));
    }

    public void Dispose()
    {
        _signingKey.Dispose();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufräumen ist best-effort.
        }
    }

    // Signiert die Paketbytes mit dem Testschlüssel (gleiches Verfahren wie die echte Signatur).
    private byte[] Sign(byte[] data) => _signingKey.SignData(data, HashAlgorithmName.SHA256);

    // Dienst ohne Download-Inhalt (für Datei-/Staging-Tests, die nicht herunterladen).
    private UpdateInstallerService CreateService(string stagingRoot, byte[]? package = null)
        => new(new FakeHttpGetClient(fileContent: package), NullLogger<UpdateInstallerService>.Instance, stagingRoot, _verifier);

    // Dienst, der Paket und passende Signatur über die jeweiligen URLs ausliefert.
    private UpdateInstallerService CreateSigningService(string stagingRoot, byte[] package)
    {
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            [PackageUrl] = package,
            [SignatureUrl] = Sign(package),
        };
        return new UpdateInstallerService(
            new FakeHttpGetClient(files),
            NullLogger<UpdateInstallerService>.Instance,
            stagingRoot,
            _verifier);
    }

    // UpdateInfo mit Paket- und Signatur-URL (Standardfall einer signierten Release).
    private static UpdateInfo SignedInfo()
        => UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, PackageUrl, SignatureUrl);

    private static byte[] BuildPackageZip()
    {
        using MemoryStream memory = new();
        using (ZipArchive archive = new(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "LookAway.exe", "neue-exe");
            WriteEntry(archive, "LookAway.Core.dll", "neue-dll");
            WriteEntry(archive, "portable.flag", string.Empty);
            WriteEntry(archive, "Assets/sound.wav", "ton");
        }

        return memory.ToArray();
    }

    private static byte[] BuildZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream memory = new();
        using (ZipArchive archive = new(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                WriteEntry(archive, name, content);
            }
        }

        return memory.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using StreamWriter writer = new(entry.Open());
        writer.Write(content);
    }

    [Fact]
    public async Task DownloadAndStage_entpackt_signiertes_Paket_in_Versionsordner()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, BuildPackageZip());

        StagedUpdate? staged = await service.DownloadAndStageAsync(SignedInfo());

        Assert.NotNull(staged);
        Assert.True(File.Exists(Path.Combine(staged!.Directory, "LookAway.exe")));
        Assert.True(File.Exists(Path.Combine(staged.Directory, "Assets", "sound.wav")));
        Assert.EndsWith("1.5.0", staged.Directory, StringComparison.Ordinal);
        Assert.Equal("1.5.0", staged.Version);
        Assert.False(string.IsNullOrEmpty(staged.ExecutableSha256));
    }

    [Fact]
    public async Task DownloadAndStage_übernimmt_die_Portable_Markierung_nicht()
    {
        // Der Helfer startet aus dem Staging-Ordner. Läge dort die Portable-Markierung,
        // hielte er sich für eine portable Installation, läse die Einstellungen neben sich
        // statt aus dem Datenverzeichnis der laufenden Installation — und lehnte sein
        // eigenes Update ab, weil er den vermerkten Datei-Hash dort nicht findet.
        string stagingRoot = Path.Combine(_root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, BuildPackageZip());

        StagedUpdate? staged = await service.DownloadAndStageAsync(SignedInfo());

        Assert.NotNull(staged);
        Assert.False(File.Exists(Path.Combine(staged!.Directory, "portable.flag")));
        Assert.True(File.Exists(Path.Combine(staged.Directory, "LookAway.exe")));
    }

    [Fact]
    public async Task DownloadAndStage_ohne_Paket_URL_liefert_null()
    {
        UpdateInstallerService service = CreateService(Path.Combine(_root, "staging"), BuildPackageZip());
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", "https://example.com/r", null);

        StagedUpdate? staged = await service.DownloadAndStageAsync(info);

        Assert.Null(staged);
    }

    [Fact]
    public async Task DownloadAndStage_ohne_Signatur_URL_liefert_null()
    {
        // Paket vorhanden, aber keine Signatur-URL: fail-closed -> kein Update.
        string stagingRoot = Path.Combine(_root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, BuildPackageZip());
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, PackageUrl, signatureAddress: null);

        Assert.Null(await service.DownloadAndStageAsync(info));
        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.5.0")));
    }

    [Fact]
    public async Task DownloadAndStage_lehnt_ungültige_Signatur_ab()
    {
        // Signatur passt nicht zum Paket (über andere Bytes erzeugt) -> Abweisung.
        string stagingRoot = Path.Combine(_root, "staging");
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            [PackageUrl] = BuildPackageZip(),
            [SignatureUrl] = Sign(new byte[] { 1, 2, 3, 4 }),
        };
        UpdateInstallerService service = new(
            new FakeHttpGetClient(files),
            NullLogger<UpdateInstallerService>.Instance,
            stagingRoot,
            _verifier);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo()));
        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.5.0")));
    }

    [Fact]
    public async Task DownloadAndStage_bei_Download_Fehler_liefert_null()
    {
        // FakeHttpGetClient ohne Inhalt -> DownloadFileAsync meldet Misserfolg.
        UpdateInstallerService service = CreateService(Path.Combine(_root, "staging"), package: null);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo()));
    }

    [Fact]
    public async Task DownloadAndStage_bei_Paket_ohne_EXE_liefert_null_und_räumt_auf()
    {
        byte[] zipWithoutExe = BuildZip(("readme.txt", "hallo"));
        string stagingRoot = Path.Combine(_root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, zipWithoutExe);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo()));
        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.5.0")));
    }

    [Fact]
    public async Task DownloadAndStage_lehnt_ZipSlip_ab_und_entkommt_nicht()
    {
        byte[] zipSlip = BuildZip(("../escape.exe", "böse"), ("LookAway.exe", "x"));
        string stagingRoot = Path.Combine(_root, "staging");
        UpdateInstallerService service = CreateSigningService(stagingRoot, zipSlip);

        Assert.Null(await service.DownloadAndStageAsync(SignedInfo()));
        // Kein Ausbruch aus dem Staging-Wurzelverzeichnis.
        Assert.False(File.Exists(Path.Combine(_root, "escape.exe")));
    }

    [Fact]
    public void FindVerified_liefert_Ordner_bei_passendem_Hash()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);
        string sha = UpdateInstallerService.ComputeFileHash(
            UpdateInstallerService.ExecutablePathIn(Path.Combine(stagingRoot, "1.6.0")));

        string? pending = service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), "1.6.0", sha);

        Assert.NotNull(pending);
        Assert.EndsWith("1.6.0", pending!, StringComparison.Ordinal);
    }

    [Fact]
    public void FindVerified_lehnt_falschen_Hash_ab()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        // Untergeschobener Ordner ohne passenden vermerkten Hash -> nicht angewendet.
        Assert.Null(service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), "1.6.0", "deadbeef"));
    }

    [Fact]
    public void FindVerified_ignoriert_ältere_oder_gleiche()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        StageVersion(stagingRoot, "1.5.0");
        UpdateInstallerService service = CreateService(stagingRoot);
        string sha = UpdateInstallerService.ComputeFileHash(
            UpdateInstallerService.ExecutablePathIn(Path.Combine(stagingRoot, "1.5.0")));

        Assert.Null(service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), "1.5.0", sha));
    }

    [Fact]
    public void FindVerified_ohne_Vermerk_liefert_null()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.Null(service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), null, null));
    }

    [Fact]
    public void CleanObsolete_entfernt_nur_alte_Ordner()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        StageVersion(stagingRoot, "1.4.0");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        service.CleanObsolete(new Version(1, 5, 0));

        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.4.0")));
        Assert.True(Directory.Exists(Path.Combine(stagingRoot, "1.6.0")));
    }

    [Fact]
    public void ApplyStagedFiles_ersetzt_Dateien_ohne_Portable_Flag_und_erhält_Benutzerdaten()
    {
        string source = Path.Combine(_root, "src");
        string target = Path.Combine(_root, "app");
        _ = Directory.CreateDirectory(source);
        _ = Directory.CreateDirectory(target);

        // Neues Paket im Staging.
        File.WriteAllText(Path.Combine(source, "LookAway.exe"), "neu");
        File.WriteAllText(Path.Combine(source, "portable.flag"), string.Empty);
        _ = Directory.CreateDirectory(Path.Combine(source, "Assets"));
        File.WriteAllText(Path.Combine(source, "Assets", "lib.dll"), "neu");

        // Zielordner mit alter EXE und Benutzerdaten.
        File.WriteAllText(Path.Combine(target, "LookAway.exe"), "alt");
        File.WriteAllText(Path.Combine(target, "settings.json"), "benutzer");

        UpdateInstallerService service = CreateService(Path.Combine(_root, "staging"));
        service.ApplyStagedFiles(source, target);

        Assert.Equal("neu", File.ReadAllText(Path.Combine(target, "LookAway.exe")));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(target, "Assets", "lib.dll")));
        // Benutzerdaten bleiben erhalten.
        Assert.Equal("benutzer", File.ReadAllText(Path.Combine(target, "settings.json")));
        // Portable-Markierung wird nicht übernommen.
        Assert.False(File.Exists(Path.Combine(target, "portable.flag")));
    }

    [Fact]
    public void IsDirectoryWritable_ist_wahr_für_Temp_und_falsch_für_Unbekannt()
    {
        Assert.True(UpdateInstallerService.IsDirectoryWritable(_root));
        Assert.False(UpdateInstallerService.IsDirectoryWritable(Path.Combine(_root, "gibt-es-nicht")));
    }

    [Fact]
    public void IsTrustedStagingDirectory_akzeptiert_Staging_Ordner_mit_passendem_Hash()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        string source = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(source);
        string exe = Path.Combine(source, "LookAway.exe");
        File.WriteAllText(exe, "neue-exe");
        string sha = UpdateInstallerService.ComputeFileHash(exe);

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.True(service.IsTrustedStagingDirectory(source, sha));
    }

    [Fact]
    public void IsTrustedStagingDirectory_lehnt_Ordner_ausserhalb_des_Staging_ab()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string outside = Path.Combine(_root, "attacker");
        _ = Directory.CreateDirectory(outside);
        string exe = Path.Combine(outside, "LookAway.exe");
        File.WriteAllText(exe, "boese-exe");
        string sha = UpdateInstallerService.ComputeFileHash(exe);

        UpdateInstallerService service = CreateService(stagingRoot);

        // Trotz passendem Hash abgelehnt, weil der Ordner nicht im Staging-Bereich liegt.
        Assert.False(service.IsTrustedStagingDirectory(outside, sha));
    }

    [Fact]
    public void IsTrustedStagingDirectory_lehnt_bei_Hash_Abweichung_ab()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        string source = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "LookAway.exe"), "manipuliert");

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.False(service.IsTrustedStagingDirectory(source, "0000000000000000000000000000000000000000000000000000000000000000"));
    }

    [Fact]
    public void IsTrustedStagingDirectory_lehnt_Traversal_über_Staging_hinaus_ab()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string exe = Path.Combine(_root, "LookAway.exe");
        File.WriteAllText(exe, "boese-exe");
        string sha = UpdateInstallerService.ComputeFileHash(exe);

        UpdateInstallerService service = CreateService(stagingRoot);

        // "staging\..\" führt aus dem Staging-Wurzelordner heraus und wird abgewiesen.
        string traversal = Path.Combine(stagingRoot, "..");
        Assert.False(service.IsTrustedStagingDirectory(traversal, sha));
    }

    [Fact]
    public void IsTrustedTargetDirectory_akzeptiert_bestehende_Installation()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string install = Path.Combine(_root, "Programs", "LookAway");
        _ = Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "LookAway.exe"), "alte-exe");

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.True(service.IsTrustedTargetDirectory(install));
    }

    [Fact]
    public void IsTrustedTargetDirectory_lehnt_den_Staging_Ordner_selbst_ab()
    {
        // Der Helfer läuft aus dem Staging-Ordner. Nähme er sein eigenes Programm-
        // verzeichnis als Ziel, kopierte er auf sich selbst und die Installation bliebe
        // unverändert — genau so blieb das Update wirkungslos.
        string stagingRoot = Path.Combine(_root, "staging");
        string staged = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(staged);
        File.WriteAllText(Path.Combine(staged, "LookAway.exe"), "neue-exe");

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.False(service.IsTrustedTargetDirectory(staged));
        Assert.False(service.IsTrustedTargetDirectory(stagingRoot));
    }

    [Fact]
    public void IsTrustedTargetDirectory_lehnt_Ordner_ohne_Programmdatei_ab()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string fremd = Path.Combine(_root, "beliebiger-ordner");
        _ = Directory.CreateDirectory(fremd);

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.False(service.IsTrustedTargetDirectory(fremd));
    }

    [Fact]
    public void IsTrustedTargetDirectory_lehnt_fehlenden_Pfad_ab()
    {
        UpdateInstallerService service = CreateService(Path.Combine(_root, "staging"));

        Assert.False(service.IsTrustedTargetDirectory(null));
        Assert.False(service.IsTrustedTargetDirectory("  "));
        Assert.False(service.IsTrustedTargetDirectory(Path.Combine(_root, "gibt-es-nicht")));
    }

    private static void StageVersion(string stagingRoot, string version)
    {
        string dir = Path.Combine(stagingRoot, version);
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "LookAway.exe"), "exe");
    }
}
