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
/// Datei-Tausch: Ersetzen ohne Portable-Markierung und unter Erhalt der Benutzerdaten, Rückrollen
/// bei gesperrten Dateien, Schreibprobe des Zielordners.
/// </summary>
public sealed class UpdateInstallerFileSwapTests : UpdateInstallerTestBase
{
    [Fact]
    public async Task ApplyStagedFiles_ReplacesFilesWithoutThePortableMarkerAndKeepsUserData()
    {
        string source = Path.Combine(Root, "src");
        string target = Path.Combine(Root, "app");
        _ = Directory.CreateDirectory(source);
        _ = Directory.CreateDirectory(target);

        // Neues Paket im Staging.
        await File.WriteAllTextAsync(Path.Combine(source, "LookAway.exe"), "neu", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(source, "portable.flag"), string.Empty, TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Path.Combine(source, "Assets"));
        await File.WriteAllTextAsync(Path.Combine(source, "Assets", "lib.dll"), "neu", TestContext.Current.CancellationToken);

        // Zielordner mit alter EXE und Benutzerdaten.
        await File.WriteAllTextAsync(Path.Combine(target, "LookAway.exe"), "alt", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(target, "settings.json"), "benutzer", TestContext.Current.CancellationToken);

        UpdateInstallerService service = CreateService(Path.Combine(Root, "staging"));
        await service.ApplyStagedFilesAsync(source, target, TestContext.Current.CancellationToken);

        Assert.Equal("neu", await File.ReadAllTextAsync(Path.Combine(target, "LookAway.exe"), TestContext.Current.CancellationToken));
        Assert.Equal("neu", await File.ReadAllTextAsync(Path.Combine(target, "Assets", "lib.dll"), TestContext.Current.CancellationToken));
        // Benutzerdaten bleiben erhalten.
        Assert.Equal("benutzer", await File.ReadAllTextAsync(Path.Combine(target, "settings.json"), TestContext.Current.CancellationToken));
        // Portable-Markierung wird nicht übernommen.
        Assert.False(File.Exists(Path.Combine(target, "portable.flag")));
    }

    /// <remarks>
    /// Ein halb eingespieltes Update darf die Installation nicht zerstören. Bleibt eine Zieldatei
    /// gesperrt, gibt der Datei-Tausch nach seinen Versuchen auf und stellt die bereits ersetzten
    /// Dateien wieder her; der Fehler geht an den Aufrufer, der ihn protokolliert.
    /// </remarks>
    [Fact]
    public async Task ApplyStagedFiles_WhenATargetStaysLocked_RetriesAndRollsBack()
    {
        string source = Path.Combine(Root, "src");
        string target = Path.Combine(Root, "app");
        _ = Directory.CreateDirectory(source);
        _ = Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "a.dll"), "neu", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(source, "z.dll"), "neu", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(target, "a.dll"), "alt", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(target, "z.dll"), "alt", TestContext.Current.CancellationToken);

        UpdateInstallerService service = new(
            new FakeHttpGetClient(fileContent: null),
            NullLogger<UpdateInstallerService>.Instance,
            Path.Combine(Root, "staging"),
            Verifier,
            copyRetryDelay: TimeSpan.Zero);

        // Lesen bleibt erlaubt, damit die Sicherung gelingt; erst das Überschreiben scheitert.
        using (FileStream readOnlyLock = new(Path.Combine(target, "z.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _ = await Assert.ThrowsAnyAsync<IOException>(() => service.ApplyStagedFilesAsync(source, target, TestContext.Current.CancellationToken));
        }

        Assert.Equal("alt", await File.ReadAllTextAsync(Path.Combine(target, "a.dll"), TestContext.Current.CancellationToken));
        Assert.Equal("alt", await File.ReadAllTextAsync(Path.Combine(target, "z.dll"), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(target, "a.dll.bak-update")));
    }

    [Fact]
    public void IsDirectoryWritable_IsTrueForTempAndFalseForAnUnknownPath()
    {
        Assert.True(UpdateInstallerService.IsDirectoryWritable(Root));
        Assert.False(UpdateInstallerService.IsDirectoryWritable(Path.Combine(Root, "gibt-es-nicht")));
    }

    [Fact]
    public void IsDirectoryWritable_IsFalseWhenTheProbeCannotBeWritten()
    {
        string occupied = Path.Combine(Root, "belegt");
        _ = Directory.CreateDirectory(Path.Combine(occupied, ".lookaway-write-probe.tmp"));
        string locked = Path.Combine(Root, "gesperrt");
        _ = Directory.CreateDirectory(locked);

        using LockedFile probe = new(Path.Combine(locked, ".lookaway-write-probe.tmp"));

        Assert.False(UpdateInstallerService.IsDirectoryWritable(occupied));
        Assert.False(UpdateInstallerService.IsDirectoryWritable(locked));
    }
}
