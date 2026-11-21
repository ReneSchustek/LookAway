using System.IO.Compression;
using System.Security.Cryptography;
using LookAway.Core.Interfaces;
using LookAway.Data.Update;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Liefert ein signiertes Update-Paket aus, wie es ein Release bereitstellt: ein Archiv mit der
/// Programmdatei und die losgelöste Signatur dazu, unterschrieben mit einem eigenen Testschlüssel.
/// </summary>
internal sealed class PackageServer : IHttpGetClient, IDisposable
{
    public const string PackageAddress = "https://example.com/LookAway.zip";
    public const string SignatureAddress = "https://example.com/LookAway.zip.sig";

    public const string SetupAddress = "https://example.com/LookAway-Setup.exe";

    public const string SetupSignatureAddress = "https://example.com/LookAway-Setup.exe.sig";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public PackageServer()
    {
        byte[] package = BuildPackage();
        _files[PackageAddress] = package;
        _files[SignatureAddress] = _key.SignData(package, HashAlgorithmName.SHA256);
        byte[] setup = "MZ-setup"u8.ToArray();
        _files[SetupAddress] = setup;
        _files[SetupSignatureAddress] = _key.SignData(setup, HashAlgorithmName.SHA256);
        Verifier = new ReleaseSignatureVerifier(Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo()));
    }

    /// <summary>Prüfer mit dem öffentlichen Teil des Testschlüssels.</summary>
    public ReleaseSignatureVerifier Verifier { get; }

    /// <summary>Zahl der Downloads.</summary>
    public int DownloadCount { get; private set; }

    public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public async Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        DownloadCount++;

        if (!_files.TryGetValue(requestUri.AbsoluteUri, out byte[]? content))
        {
            return false;
        }

        await File.WriteAllBytesAsync(destinationPath, content, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public void Dispose() => _key.Dispose();

    private static byte[] BuildPackage()
    {
        using MemoryStream memory = new();
        using (ZipArchive archive = new(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("LookAway.exe");
            using StreamWriter writer = new(entry.Open());
            writer.Write("neue-exe");
        }

        return memory.ToArray();
    }
}
