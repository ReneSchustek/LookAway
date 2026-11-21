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
/// Vertrauen in Ordner: Quellordner nur im Staging-Bereich und mit vermerktem Hash, Zielordner nur
/// als echte Installation, ausstehende Updates nur verifiziert, veraltete Stände werden abgeräumt.
/// </summary>
public sealed class UpdateInstallerTrustTests : UpdateInstallerTestBase
{
    [Fact]
    public void FindVerified_WithMatchingHash_ReturnsTheFolder()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);
        string sha = UpdateInstallerService.ComputeFileHash(
            UpdateInstallerService.ExecutablePathIn(Path.Combine(stagingRoot, "1.6.0")));

        string? pending = service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), "1.6.0", sha);

        Assert.NotNull(pending);
        Assert.EndsWith("1.6.0", pending!, StringComparison.Ordinal);
    }

    [Fact]
    public void FindVerified_RejectsAWrongHash()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        // Untergeschobener Ordner ohne passenden vermerkten Hash -> nicht angewendet.
        Assert.Null(service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), "1.6.0", "deadbeef"));
    }

    [Fact]
    public void FindVerified_IgnoresOlderOrEqualVersions()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.5.0");
        UpdateInstallerService service = CreateService(stagingRoot);
        string sha = UpdateInstallerService.ComputeFileHash(
            UpdateInstallerService.ExecutablePathIn(Path.Combine(stagingRoot, "1.5.0")));

        Assert.Null(service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), "1.5.0", sha));
    }

    [Fact]
    public void FindVerified_WithoutARecord_ReturnsNull()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.Null(service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), null, null));
    }

    [Fact]
    public void CleanObsolete_RemovesOnlyOutdatedFolders()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.4.0");
        StageVersion(stagingRoot, "1.6.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        service.CleanObsolete(new Version(1, 5, 0));

        Assert.False(Directory.Exists(Path.Combine(stagingRoot, "1.4.0")));
        Assert.True(Directory.Exists(Path.Combine(stagingRoot, "1.6.0")));
    }

    [Fact]
    public void IsTrustedStagingDirectory_AcceptsAStagingFolderWithMatchingHash()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        string source = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(source);
        string exe = Path.Combine(source, "LookAway.exe");
        File.WriteAllText(exe, "neue-exe");
        string sha = UpdateInstallerService.ComputeFileHash(exe);

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.True(service.IsTrustedStagingDirectory(source, sha));
    }

    [Fact]
    public void IsTrustedStagingDirectory_RejectsFoldersOutsideTheStagingArea()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string outside = Path.Combine(Root, "attacker");
        _ = Directory.CreateDirectory(outside);
        string exe = Path.Combine(outside, "LookAway.exe");
        File.WriteAllText(exe, "böse-exe");
        string sha = UpdateInstallerService.ComputeFileHash(exe);

        UpdateInstallerService service = CreateService(stagingRoot);

        // Trotz passendem Hash abgelehnt, weil der Ordner nicht im Staging-Bereich liegt.
        Assert.False(service.IsTrustedStagingDirectory(outside, sha));
    }

    [Fact]
    public void IsTrustedStagingDirectory_RejectsOnHashMismatch()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        string source = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "LookAway.exe"), "manipuliert");

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.False(service.IsTrustedStagingDirectory(source, "0000000000000000000000000000000000000000000000000000000000000000"));
    }

    [Fact]
    public void IsTrustedStagingDirectory_RejectsTraversalBeyondTheStagingArea()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string exe = Path.Combine(Root, "LookAway.exe");
        File.WriteAllText(exe, "böse-exe");
        string sha = UpdateInstallerService.ComputeFileHash(exe);

        UpdateInstallerService service = CreateService(stagingRoot);

        // "staging\..\" führt aus dem Staging-Wurzelordner heraus und wird abgewiesen.
        string traversal = Path.Combine(stagingRoot, "..");
        Assert.False(service.IsTrustedStagingDirectory(traversal, sha));
    }

    [Fact]
    public void IsTrustedTargetDirectory_AcceptsAnExistingInstallation()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string install = Path.Combine(Root, "Programs", "LookAway");
        _ = Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "LookAway.exe"), "alte-exe");

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.True(service.IsTrustedTargetDirectory(install));
    }

    [Fact]
    public void IsTrustedTargetDirectory_RejectsTheStagingFolderItself()
    {
        // Der Helfer läuft aus dem Staging-Ordner. Nähme er sein eigenes Programm-
        // verzeichnis als Ziel, kopierte er auf sich selbst und die Installation bliebe
        // unverändert — genau so blieb das Update wirkungslos.
        string stagingRoot = Path.Combine(Root, "staging");
        string staged = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(staged);
        File.WriteAllText(Path.Combine(staged, "LookAway.exe"), "neue-exe");

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.False(service.IsTrustedTargetDirectory(staged));
        Assert.False(service.IsTrustedTargetDirectory(stagingRoot));
    }

    [Fact]
    public void IsTrustedTargetDirectory_RejectsFoldersWithoutTheExecutable()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        _ = Directory.CreateDirectory(stagingRoot);
        string fremd = Path.Combine(Root, "beliebiger-ordner");
        _ = Directory.CreateDirectory(fremd);

        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.False(service.IsTrustedTargetDirectory(fremd));
    }

    [Fact]
    public void IsTrustedTargetDirectory_RejectsAMissingPath()
    {
        UpdateInstallerService service = CreateService(Path.Combine(Root, "staging"));

        Assert.False(service.IsTrustedTargetDirectory(null));
        Assert.False(service.IsTrustedTargetDirectory("  "));
        Assert.False(service.IsTrustedTargetDirectory(Path.Combine(Root, "gibt-es-nicht")));
    }

    [Theory]
    [InlineData(null, "abc")]
    [InlineData("  ", "abc")]
    [InlineData("staging", null)]
    [InlineData("staging", " ")]
    public void IsTrustedStagingDirectory_RejectsMissingArguments(string? folder, string? sha)
    {
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateService(stagingRoot);
        string? source = folder == "staging" ? Path.Combine(stagingRoot, "1.5.0") : folder;

        Assert.False(service.IsTrustedStagingDirectory(source, sha));
    }

    [Fact]
    public void IsTrustedStagingDirectory_RejectsAFolderWithoutTheExecutable()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        string source = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(source);
        UpdateInstallerService service = CreateService(stagingRoot);

        Assert.False(service.IsTrustedStagingDirectory(source, "abc"));
    }

    /// <remarks>Eine Programmdatei, die sich nicht lesen lässt, ist ebenso wenig geprüft wie eine falsche.</remarks>
    [Fact]
    public void IsTrustedStagingDirectory_RejectsAnUnreadableExecutable()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        string source = Path.Combine(stagingRoot, "1.5.0");
        _ = Directory.CreateDirectory(source);
        string exe = Path.Combine(source, "LookAway.exe");
        File.WriteAllText(exe, "neue-exe");
        string sha = UpdateInstallerService.ComputeFileHash(exe);
        UpdateInstallerService service = CreateService(stagingRoot);

        using LockedFile locked = new(exe, "neue-exe");

        Assert.False(service.IsTrustedStagingDirectory(source, sha));
    }

    /// <remarks>
    /// Die Pfade kommen von der Befehlszeile des Helfers. Einen Pfad, den das System gar nicht
    /// erst annimmt, weist die Prüfung ab, statt mit einer Ausnahme auszusteigen.
    /// </remarks>
    [Fact]
    public void TrustChecks_RejectAPathTheSystemDoesNotAccept()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateService(stagingRoot);
        string invalid = Path.Combine(stagingRoot, "1.5.0") + "\0";

        Assert.False(service.IsTrustedStagingDirectory(invalid, "abc"));
        Assert.False(service.IsTrustedTargetDirectory(invalid));
    }

    [Fact]
    public void FindVerified_WithAnUnreadableExecutable_ReturnsNull()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.6.0");
        string exe = UpdateInstallerService.ExecutablePathIn(Path.Combine(stagingRoot, "1.6.0"));
        string sha = UpdateInstallerService.ComputeFileHash(exe);
        UpdateInstallerService service = CreateService(stagingRoot);

        using LockedFile locked = new(exe, "exe");

        Assert.Null(service.FindVerifiedPendingUpdateDirectory(new Version(1, 5, 0), "1.6.0", sha));
    }

    [Fact]
    public void CleanObsolete_WithoutStagingFolder_DoesNothing()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        UpdateInstallerService service = CreateService(stagingRoot);

        service.CleanObsolete(new Version(1, 5, 0));

        Assert.False(Directory.Exists(stagingRoot));
    }

    [Fact]
    public void CleanObsolete_LeavesALockedFolderForTheNextRun()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.4.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        using (LockedFile locked = new(Path.Combine(stagingRoot, "1.4.0", "LookAway.exe"), "exe"))
        {
            service.CleanObsolete(new Version(1, 5, 0));
        }

        Assert.True(Directory.Exists(Path.Combine(stagingRoot, "1.4.0")));
    }

    /// <remarks>Ein Staging-Ordner, den der Benutzer nicht lesen darf, hält den Programmstart nicht auf.</remarks>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void CleanObsolete_WithAnUnreadableStagingFolder_DoesNotThrow()
    {
        string stagingRoot = Path.Combine(Root, "staging");
        StageVersion(stagingRoot, "1.4.0");
        UpdateInstallerService service = CreateService(stagingRoot);

        using (UnlistableFolder denied = new(stagingRoot))
        {
            Exception? thrown = Record.Exception(() => service.CleanObsolete(new Version(1, 5, 0)));

            Assert.Null(thrown);
        }

        Assert.True(Directory.Exists(Path.Combine(stagingRoot, "1.4.0")));
    }
}
