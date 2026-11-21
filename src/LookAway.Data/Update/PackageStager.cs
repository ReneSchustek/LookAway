using System.Security.Cryptography;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Update;

/// <summary>
/// Lädt das Paket eines Updates samt losgelöster Signatur, prüft die Echtheit und entpackt es in
/// einen versionsbenannten Staging-Ordner. Ohne gültige Signatur wird nichts entpackt.
/// </summary>
internal sealed class PackageStager
{
    private readonly IHttpGetClient _httpClient;
    private readonly ReleaseSignatureVerifier _signatureVerifier;
    private readonly string _stagingRoot;
    private readonly ILogger _logger;

    /// <summary>Erzeugt den Bereitsteller.</summary>
    /// <param name="httpClient">HTTP-Zugriff für Paket und Signatur.</param>
    /// <param name="signatureVerifier">Prüfer der losgelösten Paket-Signatur.</param>
    /// <param name="stagingRoot">Wurzelordner für entpackte Update-Pakete.</param>
    /// <param name="logger">Protokolliert Ergebnis und Fehler.</param>
    public PackageStager(IHttpGetClient httpClient, ReleaseSignatureVerifier signatureVerifier, string stagingRoot, ILogger logger)
    {
        _httpClient = httpClient;
        _signatureVerifier = signatureVerifier;
        _stagingRoot = stagingRoot;
        _logger = logger;
    }

    /// <summary>
    /// Lädt das Portable-Paket herunter und entpackt es in einen versionsbenannten Staging-Ordner.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Staging-Ordner, Version und Datei-Hash bei Erfolg, sonst <c>null</c>.</returns>
    public async Task<StagedUpdate?> DownloadAndStageAsync(UpdateInfo info, CancellationToken cancellationToken)
    {
        if (info.PackageUrl is null)
        {
            return null;
        }

        StagingPaths paths = StagingPaths.Create(_stagingRoot, info.LatestVersion);
        try
        {
            _ = Directory.CreateDirectory(_stagingRoot);

            if (!await DownloadVerifiedAsync(info, info.PackageUrl, paths, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            await Task.Run(() => SafeArchiveExtractor.ExtractTo(paths.Package, paths.Work), cancellationToken).ConfigureAwait(false);
            File.Delete(paths.Package);
            return MoveIntoPlace(info.LatestVersion, paths);
        }
        // Datei-, Zugriffs-, Archiv- und Signaturfehler bedeuten alle dasselbe:
        // das Paket ist unbrauchbar. Die laufende App darf daran nie scheitern.
        catch (Exception ex) when (IsStagingFailure(ex))
        {
            UpdateInstallerLog.StageFailed(_logger, ex, info.LatestVersion);
            paths.CleanUp();
            return null;
        }
    }

    private static bool IsStagingFailure(Exception exception) => exception
        is IOException
        or UnauthorizedAccessException
        or InvalidDataException
        or NotSupportedException
        or CryptographicException;

    // Lädt das Paket und prüft seine Echtheit, bevor irgendetwas entpackt wird. Ohne losgelöste
    // Signatur, die unter dem eingebetteten Schlüssel zum Paket passt, gilt es als gescheitert
    // (fail-closed). Die Teildatei eines gescheiterten Laufs wird hier aufgeräumt, weil der
    // Download Misserfolg meldet, ohne zu werfen.
    private async Task<bool> DownloadVerifiedAsync(UpdateInfo info, Uri packageUrl, StagingPaths paths, CancellationToken cancellationToken)
    {
        bool verified = await _httpClient.DownloadFileAsync(packageUrl, paths.Package, cancellationToken).ConfigureAwait(false)
            && await VerifyPackageSignatureAsync(info, paths, cancellationToken).ConfigureAwait(false);
        if (!verified)
        {
            UpdateFiles.TryDelete(paths.Package);
        }

        return verified;
    }

    // Lädt die losgelöste Signatur, prüft sie gegen das Paket und entfernt sie danach.
    private async Task<bool> VerifyPackageSignatureAsync(UpdateInfo info, StagingPaths paths, CancellationToken cancellationToken)
    {
        if (info.SignatureUrl is null
            || !await _httpClient.DownloadFileAsync(info.SignatureUrl, paths.Signature, cancellationToken).ConfigureAwait(false))
        {
            UpdateInstallerLog.SignatureMissing(_logger, info.LatestVersion);
            UpdateFiles.TryDelete(paths.Signature);
            return false;
        }

        byte[] signature = await File.ReadAllBytesAsync(paths.Signature, cancellationToken).ConfigureAwait(false);
        UpdateFiles.TryDelete(paths.Signature);

        if (!_signatureVerifier.VerifyFile(paths.Package, signature))
        {
            UpdateInstallerLog.SignatureInvalid(_logger, info.LatestVersion);
            return false;
        }

        UpdateInstallerLog.SignatureVerified(_logger, info.LatestVersion);
        return true;
    }

    // Versetzt den entpackten Arbeitsordner an den endgültigen, versionsbenannten Ort. Ein Paket
    // ohne Programmdatei ist unvollständig und wird verworfen.
    private StagedUpdate? MoveIntoPlace(string version, StagingPaths paths)
    {
        if (!File.Exists(UpdateFiles.ExecutablePathIn(paths.Work)))
        {
            UpdateInstallerLog.PackageInvalid(_logger, paths.Work);
            UpdateFiles.TryDeleteDirectory(paths.Work);
            return null;
        }

        if (Directory.Exists(paths.Final))
        {
            Directory.Delete(paths.Final, recursive: true);
        }

        Directory.Move(paths.Work, paths.Final);

        string sha = UpdateFiles.ComputeFileHash(UpdateFiles.ExecutablePathIn(paths.Final));
        UpdateInstallerLog.Staged(_logger, version, paths.Final);
        return new StagedUpdate(paths.Final, version, sha);
    }

    // Eindeutige Arbeitspfade je Lauf: verhindern Kollisionen, wenn Auto- und manuelles Update
    // parallel dasselbe Paket laden. Nur der endgültige Ordner trägt die Version.
    private sealed record StagingPaths(string Final, string Package, string Signature, string Work)
    {
        public static StagingPaths Create(string stagingRoot, string version)
        {
            string token = Guid.NewGuid().ToString("N");
            return new StagingPaths(
                Path.Combine(stagingRoot, version),
                Path.Combine(stagingRoot, $"_dl-{token}.zip"),
                Path.Combine(stagingRoot, $"_dl-{token}.sig"),
                Path.Combine(stagingRoot, $"_work-{token}"));
        }

        // Räumt die Arbeitspfade eines gescheiterten Laufs ab; der endgültige Ordner bleibt.
        public void CleanUp()
        {
            UpdateFiles.TryDelete(Package);
            UpdateFiles.TryDelete(Signature);
            UpdateFiles.TryDeleteDirectory(Work);
        }
    }
}
