using System.Diagnostics.CodeAnalysis;
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
/// Gemeinsame Ausstattung der Installer-Tests: ein eigener Temp-Ordner, ein ECDSA-Testschlüssel, mit
/// dem die Tests ihre Pakete signieren, und Hilfen für Pakete, Dienste und Staging-Ordner.
/// </summary>
/// <remarks>
/// Der Installer spielt Pakete nur mit gültiger Signatur ein (fail-closed). Der Dienst bekommt deshalb
/// einen <see cref="ReleaseSignatureVerifier"/> mit dem öffentlichen Teil des Testschlüssels.
/// </remarks>
[SuppressMessage("Maintainability", "CA1515", Justification = "xUnit findet nur öffentliche Testklassen, und eine öffentliche Klasse kann nicht von einer internen erben.")]
public abstract class UpdateInstallerTestBase : IDisposable
{
    protected const string PackageUrl = "https://example.com/p.zip";
    protected const string SignatureUrl = "https://example.com/p.zip.sig";

    protected string Root { get; }
    private readonly ECDsa _signingKey;
    protected ReleaseSignatureVerifier Verifier { get; }

    protected UpdateInstallerTestBase()
    {
        Root = Path.Combine(Path.GetTempPath(), "lookaway-update-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Root);

        _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Verifier = new ReleaseSignatureVerifier(Convert.ToBase64String(_signingKey.ExportSubjectPublicKeyInfo()));
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _signingKey.Dispose();
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufräumen ist best-effort.
        }
    }

    // Signiert die Paketbytes mit dem Testschlüssel (gleiches Verfahren wie die echte Signatur).
    protected byte[] Sign(byte[] data) => _signingKey.SignData(data, HashAlgorithmName.SHA256);

    // Dienst ohne Download-Inhalt (für Datei-/Staging-Tests, die nicht herunterladen).
    protected UpdateInstallerService CreateService(string stagingRoot, byte[]? package = null)
        => new(new FakeHttpGetClient(fileContent: package), NullLogger<UpdateInstallerService>.Instance, stagingRoot, Verifier);

    // Dienst, der Paket und passende Signatur über die jeweiligen URLs ausliefert.
    protected UpdateInstallerService CreateSigningService(string stagingRoot, byte[] package)
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
            Verifier);
    }

    // UpdateInfo mit Paket- und Signatur-URL (Standardfall einer signierten Release).
    protected static UpdateInfo SignedInfo()
        => UpdateInfo.Create(new Version(1, 0, 0), "v1.5.0", null, null, new ReleaseAssets(PackageUrl, SignatureUrl));

    protected static byte[] BuildPackageZip()
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

    protected static byte[] BuildZip(params (string Name, string Content)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

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

    protected static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ArgumentNullException.ThrowIfNull(archive);

        ZipArchiveEntry entry = archive.CreateEntry(name);
        using StreamWriter writer = new(entry.Open());
        writer.Write(content);
    }


    // Meldet jeden Download als gescheitert, hält die angelegte Teildatei aber gesperrt.
    protected sealed class LockingDownloadClient : IHttpGetClient, IDisposable
    {
        private LockedFile? _lock;

        public string? LockedPath { get; private set; }

        public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
        {
            _lock = new LockedFile(destinationPath, "teil");
            LockedPath = destinationPath;
            return Task.FromResult(false);
        }

        public void Dispose() => _lock?.Dispose();
    }

    protected static void StageVersion(string stagingRoot, string version)
    {
        string dir = Path.Combine(stagingRoot, version);
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "LookAway.exe"), "exe");
    }
}
