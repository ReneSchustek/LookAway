using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Update;

/// <summary>
/// Stellt das Setup einer Aktualisierung bereit: lädt es samt losgelöster Signatur in den
/// versionsbenannten Staging-Ordner und prüft die Signatur an der gesperrten Datei. Ohne gültige
/// Signatur gibt es kein Setup.
/// </summary>
internal sealed class SetupStager
{
    // Fester Name statt des Namens aus dem Release: Der Ordner trägt schon die Version, und ein
    // fremd gewählter Dateiname hätte im Dateisystem nichts zu suchen.
    private const string SetupFileName = "LookAway-Setup.exe";

    private readonly IHttpGetClient _httpClient;
    private readonly ReleaseSignatureVerifier _signatureVerifier;
    private readonly string _stagingRoot;
    private readonly ILogger _logger;

    /// <summary>Erzeugt den Bereitsteller.</summary>
    /// <param name="httpClient">HTTP-Zugriff für Setup und Signatur.</param>
    /// <param name="signatureVerifier">Prüfer der losgelösten Signatur.</param>
    /// <param name="stagingRoot">Wurzelordner, unter dem je Version ein Ordner entsteht.</param>
    /// <param name="logger">Protokolliert Ergebnis und Fehler.</param>
    public SetupStager(IHttpGetClient httpClient, ReleaseSignatureVerifier signatureVerifier, string stagingRoot, ILogger logger)
    {
        _httpClient = httpClient;
        _signatureVerifier = signatureVerifier;
        _stagingRoot = stagingRoot;
        _logger = logger;
    }

    /// <summary>
    /// Lädt Setup und Signatur, sofern sie noch nicht bereitliegen, und prüft die Signatur an der
    /// gesperrten Datei.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Das gesperrte, geprüfte Setup, oder <c>null</c>.</returns>
    public async Task<VerifiedSetup?> PrepareAsync(UpdateInfo info, CancellationToken cancellationToken)
    {
        if (info.SetupUrl is null || info.SetupSignatureUrl is null)
        {
            UpdateInstallerLog.SetupMissing(_logger, info.LatestVersion);
            return null;
        }

        string setup = Path.Combine(_stagingRoot, info.LatestVersion, SetupFileName);
        string signature = setup + ".sig";
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(setup)!); // Pfad aus Wurzel, Version und Dateiname
            if (!await EnsureDownloadedAsync(info.SetupUrl, setup, cancellationToken).ConfigureAwait(false)
                || !await EnsureDownloadedAsync(info.SetupSignatureUrl, signature, cancellationToken).ConfigureAwait(false))
            {
                UpdateInstallerLog.SetupDownloadFailed(_logger, info.LatestVersion);
                return null;
            }

            return await OpenVerifiedAsync(setup, signature, info.LatestVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UpdateInstallerLog.SetupPrepareFailed(_logger, ex, info.LatestVersion);
            return null;
        }
    }

    // Lädt in eine Teildatei und benennt erst nach vollständigem Download um: Eine abgebrochene
    // Übertragung hinterlässt so nie eine scheinbar fertige Datei, die beim nächsten Mal als
    // „schon geladen“ gälte. Eine vorhandene Datei wird nicht erneut geladen; ihre Echtheit prüft
    // die Signatur ohnehin bei jeder Verwendung.
    private async Task<bool> EnsureDownloadedAsync(Uri address, string path, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            return true;
        }

        string partial = $"{path}.part-{Guid.NewGuid():N}";
        if (!await _httpClient.DownloadFileAsync(address, partial, cancellationToken).ConfigureAwait(false))
        {
            UpdateFiles.TryDelete(partial);
            return false;
        }

        File.Move(partial, path, overwrite: true);
        return true;
    }

    // Öffnet das Setup so, dass niemand es mehr ändern kann, und prüft die Signatur an genau diesem
    // offenen Datenstrom. Eine ungültige Signatur verwirft beide Dateien, damit der nächste Versuch
    // neu lädt statt dieselben Bytes wieder abzulehnen.
    private async Task<VerifiedSetup?> OpenVerifiedAsync(string setup, string signature, string version, CancellationToken cancellationToken)
    {
        byte[] signatureBytes = await File.ReadAllBytesAsync(signature, cancellationToken).ConfigureAwait(false);
        return OpenVerified(setup, signature, signatureBytes, version);
    }

    private VerifiedSetup? OpenVerified(string setup, string signature, byte[] signatureBytes, string version)
    {
        FileStream? locked = new(setup, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (!_signatureVerifier.VerifyStream(locked, signatureBytes))
            {
                locked.Dispose();
                locked = null;
                UpdateFiles.TryDelete(setup);
                UpdateFiles.TryDelete(signature);
                UpdateInstallerLog.SignatureInvalid(_logger, version);
                return null;
            }

            UpdateInstallerLog.SetupVerified(_logger, version);
            VerifiedSetup verified = new(setup, version, locked);
            locked = null; // gehört jetzt dem VerifiedSetup
            return verified;
        }
        finally
        {
            locked?.Dispose();
        }
    }
}
