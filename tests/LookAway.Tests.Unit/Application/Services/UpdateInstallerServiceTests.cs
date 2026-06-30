using System.IO.Compression;
using LookAway.Application.Services;
using LookAway.Core.ValueObjects;
using LookAway.Tests.Unit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Unit.Application.Services;

/// <summary>
/// Tests fuer den <see cref="UpdateInstallerService"/>: Download/Entpacken in den
/// Staging-Ordner, Erkennung ausstehender Updates und der Datei-Tausch (inkl.
/// Auslassen der Portable-Markierung und Erhalt von Benutzerdaten).
/// </summary>
public sealed class UpdateInstallerServiceTests : IDisposable
{
    private readonly string _root;

    public UpdateInstallerServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lookaway-update-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufraeumen ist best-effort.
        }
    }

    private UpdateInstallerService CreateService(string stagingRoot, byte[]? package = null)
        => new(new FakeHttpGetClient(fileContent: package), NullLogger<UpdateInstallerService>.Instance, stagingRoot);

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
    public async Task DownloadAndStage_entpackt_Paket_in_Versionsordner()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        UpdateInstallerService service = CreateService(stagingRoot, BuildPackageZip());
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, "https://example.com/p.zip");

        string? staged = await service.DownloadAndStageAsync(info);

        Assert.NotNull(staged);
        Assert.True(File.Exists(Path.Combine(staged!, "LookAway.exe")));
        Assert.True(File.Exists(Path.Combine(staged!, "Assets", "sound.wav")));
        Assert.EndsWith("1.5.0", staged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadAndStage_ohne_Paket_URL_liefert_null()
    {
        UpdateInstallerService service = CreateService(Path.Combine(_root, "staging"), BuildPackageZip());
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", "https://example.com/r", null);

        string? staged = await service.DownloadAndStageAsync(info);

        Assert.Null(staged);
    }

    [Fact]
    public async Task DownloadAndStage_bei_Download_Fehler_liefert_null()
    {
        // FakeHttpGetClient ohne Inhalt -> DownloadFileAsync meldet Misserfolg.
        UpdateInstallerService service = CreateService(Path.Combine(_root, "staging"), package: null);
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, "https://example.com/p.zip");

        Assert.Null(await service.DownloadAndStageAsync(info));
    }

    [Fact]
    public async Task DownloadAndStage_bei_Paket_ohne_EXE_liefert_null_und_raeumt_auf()
    {
        byte[] zipWithoutExe = BuildZip(("readme.txt", "hallo"));
        string stagingRoot = Path.Combine(_root, "staging");
        UpdateInstallerService service = CreateService(stagingRoot, zipWithoutExe);
        UpdateInfo info = UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, "https://example.com/p.zip");

        Assert.Null(await service.DownloadAndStageAsync(info));
        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.5.0")));
    }

    [Fact]
    public void FindPending_liefert_hoechste_neuere_Version()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        StageVersion(stagingRoot, "1.4.0");
        StageVersion(stagingRoot, "1.6.0");
        StageVersion(stagingRoot, "1.5.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        string? pending = service.FindPendingUpdateDirectory(new Version(1, 5, 0));

        Assert.NotNull(pending);
        Assert.EndsWith("1.6.0", pending, StringComparison.Ordinal);
    }

    [Fact]
    public void FindPending_ignoriert_aeltere_oder_gleiche()
    {
        string stagingRoot = Path.Combine(_root, "staging");
        StageVersion(stagingRoot, "1.4.0");
        StageVersion(stagingRoot, "1.5.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.Null(service.FindPendingUpdateDirectory(new Version(1, 5, 0)));
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
    public void ApplyStagedFiles_ersetzt_Dateien_ohne_Portable_Flag_und_erhaelt_Benutzerdaten()
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
        // Portable-Markierung wird nicht uebernommen.
        Assert.False(File.Exists(Path.Combine(target, "portable.flag")));
    }

    [Fact]
    public void IsDirectoryWritable_ist_wahr_fuer_Temp_und_falsch_fuer_Unbekannt()
    {
        Assert.True(UpdateInstallerService.IsDirectoryWritable(_root));
        Assert.False(UpdateInstallerService.IsDirectoryWritable(Path.Combine(_root, "gibt-es-nicht")));
    }

    private static void StageVersion(string stagingRoot, string version)
    {
        string dir = Path.Combine(stagingRoot, version);
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "LookAway.exe"), "exe");
    }
}
