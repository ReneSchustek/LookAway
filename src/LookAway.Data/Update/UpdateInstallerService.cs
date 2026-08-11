using System.IO.Compression;
using System.Security.Cryptography;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Update;

/// <summary>
/// Verwaltet die automatische Aktualisierung über die Portable-ZIP eines
/// GitHub-Release: lädt das Paket herunter, entpackt es in einen Staging-Ordner
/// und ersetzt — beim nächsten Start, ausgelöst vom Anwendungs-Bootstrap — die
/// Programmdateien. Die reine Datei-/Versionslogik ist hier gekapselt; das
/// Beenden/Neustarten der Prozesse steuert die App-Schicht.
/// </summary>
public sealed class UpdateInstallerService : IUpdateInstaller
{
    private const string ExecutableName = "LookAway.exe";
    private const string BackupSuffix = ".bak-update";

    // 10 Versuche × 500 ms = bis zu ~5 s Wartezeit, damit die zu ersetzende, gerade
    // beendete Vorgänger-Instanz ihre Programmdateien sicher freigibt (Virenscanner/OS).
    private const int CopyRetries = 10;
    private static readonly TimeSpan CopyRetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly IHttpGetClient _httpClient;
    private readonly ILogger<UpdateInstallerService> _logger;
    private readonly string _stagingRoot;
    private readonly ReleaseSignatureVerifier _signatureVerifier;

    /// <summary>Erzeugt den Dienst.</summary>
    /// <param name="httpClient">HTTP-Zugriff für den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    public UpdateInstallerService(IHttpGetClient httpClient, ILogger<UpdateInstallerService> logger)
        : this(httpClient, logger, DefaultStagingRoot(), new ReleaseSignatureVerifier())
    {
    }

    /// <summary>Konstruktor mit explizitem Staging-Wurzelpfad (für Tests).</summary>
    /// <param name="httpClient">HTTP-Zugriff für den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="stagingRoot">Wurzelordner für entpackte Update-Pakete.</param>
    public UpdateInstallerService(IHttpGetClient httpClient, ILogger<UpdateInstallerService> logger, string stagingRoot)
        : this(httpClient, logger, stagingRoot, new ReleaseSignatureVerifier())
    {
    }

    /// <summary>Konstruktor mit explizitem Staging-Pfad und Signaturprüfer (für Tests).</summary>
    /// <param name="httpClient">HTTP-Zugriff für den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="stagingRoot">Wurzelordner für entpackte Update-Pakete.</param>
    /// <param name="signatureVerifier">Prüfer der losgelösten Paket-Signatur.</param>
    public UpdateInstallerService(
        IHttpGetClient httpClient,
        ILogger<UpdateInstallerService> logger,
        string stagingRoot,
        ReleaseSignatureVerifier signatureVerifier)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(signatureVerifier);

        _httpClient = httpClient;
        _logger = logger;
        _stagingRoot = stagingRoot;
        _signatureVerifier = signatureVerifier;
    }

    /// <summary>Wurzelordner, unter dem Update-Pakete je Version entpackt werden.</summary>
    public string StagingRoot => _stagingRoot;

    /// <summary>Pfad der ausführbaren Datei innerhalb eines Staging-Ordners.</summary>
    public static string ExecutablePathIn(string directory) => Path.Combine(directory, ExecutableName);

    /// <summary>
    /// Prüft, ob ein Staging-Ordner vertrauenswürdig ist: Er muss innerhalb des
    /// bekannten Staging-Wurzelordners liegen (kein untergeschobener Fremdpfad) und
    /// seine Programmdatei muss den erwarteten, zuvor signaturgeprüft vermerkten
    /// SHA-256 tragen. Der Helfer-Prozess ruft dies auf, bevor er Dateien ersetzt —
    /// so wird die Signaturprüfung des Hauptprozesses auch im Helfer durchgesetzt.
    /// </summary>
    /// <param name="sourceDir">Zu prüfender Quell-/Staging-Ordner.</param>
    /// <param name="expectedSha256">Erwarteter SHA-256 der Programmdatei (aus den Einstellungen).</param>
    /// <returns><c>true</c>, wenn Ordner und Datei-Hash vertrauenswürdig sind.</returns>
    public bool IsTrustedStagingDirectory(string? sourceDir, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(expectedSha256))
        {
            return false;
        }

        try
        {
            string rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_stagingRoot));
            string sourceFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDir));

            // Der Quellpfad muss echt unterhalb des Staging-Wurzelordners liegen.
            if (!sourceFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                UpdateInstallerLog.UntrustedSource(_logger, sourceFull);
                return false;
            }

            string exe = ExecutablePathIn(sourceFull);
            if (!File.Exists(exe))
            {
                return false;
            }

            string actual = ComputeFileHash(exe);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                UpdateInstallerLog.HashMismatch(_logger, sourceFull);
                return false;
            }

            return true;
        }
        catch (IOException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return false;
        }
    }

    /// <summary>
    /// Prüft, ob der Zielordner eine echte Installation ist, die ersetzt werden darf.
    /// Der Helfer läuft aus dem Staging-Ordner und kann sein Ziel deshalb nicht aus dem
    /// eigenen Programmpfad ableiten — er bekommt es übergeben und darf ihm nicht blind
    /// vertrauen. Erlaubt ist nur ein vorhandener, ohne erhöhte Rechte beschreibbarer
    /// Ordner, der bereits die Programmdatei enthält und außerhalb des Staging-Bereichs
    /// liegt (sonst wäre die Quelle ihr eigenes Ziel).
    /// </summary>
    /// <param name="targetDir">Zu prüfender Zielordner (Programmverzeichnis).</param>
    /// <returns><c>true</c>, wenn der Ordner ersetzt werden darf.</returns>
    public bool IsTrustedTargetDirectory(string? targetDir)
    {
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            return false;
        }

        try
        {
            string rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_stagingRoot));
            string targetFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDir));

            bool insideStaging = targetFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase)
                || targetFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (insideStaging || !File.Exists(ExecutablePathIn(targetFull)) || !IsDirectoryWritable(targetFull))
            {
                UpdateInstallerLog.UntrustedTarget(_logger, targetFull);
                return false;
            }

            return true;
        }
        catch (IOException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return false;
        }
    }

    private static string DefaultStagingRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppPaths.AppFolderName,
            "updates");

    /// <summary>
    /// Lädt das Portable-Paket der angegebenen Aktualisierung herunter und
    /// entpackt es in einen versionsbenannten Staging-Ordner.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung (mit <see cref="UpdateInfo.PackageUrl"/>).</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Staging-Ordner, Version und Datei-Hash bei Erfolg, sonst <c>null</c>.</returns>
    public async Task<StagedUpdate?> DownloadAndStageAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (info.PackageUrl is null)
        {
            return null;
        }

        string stagingDir = Path.Combine(_stagingRoot, info.LatestVersion);

        // Eindeutige Arbeitspfade: verhindern Kollisionen, wenn Auto- und manuelles
        // Update parallel dasselbe Paket laden.
        string token = Guid.NewGuid().ToString("N");
        string tempZip = Path.Combine(_stagingRoot, $"_dl-{token}.zip");
        string tempSig = Path.Combine(_stagingRoot, $"_dl-{token}.sig");
        string workDir = Path.Combine(_stagingRoot, $"_work-{token}");

        try
        {
            _ = Directory.CreateDirectory(_stagingRoot);

            if (!await _httpClient.DownloadFileAsync(info.PackageUrl, tempZip, cancellationToken).ConfigureAwait(false))
            {
                // DownloadFileAsync meldet Misserfolg ohne zu werfen — die evtl.
                // angelegte Teildatei hier selbst aufräumen (catch greift nicht).
                TryDelete(tempZip);
                return null;
            }

            // Echtheit prüfen, bevor irgendetwas entpackt oder eingespielt wird:
            // ohne losgelöste Signatur, die unter dem eingebetteten Schlüssel zum
            // heruntergeladenen Paket passt, wird das Update abgewiesen (fail-closed).
            if (!await VerifyPackageSignatureAsync(info, tempZip, tempSig, cancellationToken).ConfigureAwait(false))
            {
                TryDelete(tempZip);
                return null;
            }

            await Task.Run(() => SafeArchiveExtractor.ExtractTo(tempZip, workDir), cancellationToken).ConfigureAwait(false);
            File.Delete(tempZip);

            if (!File.Exists(ExecutablePathIn(workDir)))
            {
                UpdateInstallerLog.PackageInvalid(_logger, workDir);
                TryDeleteDirectory(workDir);
                return null;
            }

            // Atomar an den endgültigen, versionsbenannten Ort versetzen.
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }

            Directory.Move(workDir, stagingDir);

            string sha = ComputeFileHash(ExecutablePathIn(stagingDir));
            UpdateInstallerLog.Staged(_logger, info.LatestVersion, stagingDir);
            return new StagedUpdate(stagingDir, info.LatestVersion, sha);
        }
        // Datei-, Zugriffs-, Archiv- und Signaturfehler bedeuten alle dasselbe:
        // das Paket ist unbrauchbar. Die laufende App darf daran nie scheitern.
        catch (Exception ex) when (IsStagingFailure(ex))
        {
            UpdateInstallerLog.StageFailed(_logger, ex, info.LatestVersion);
            TryDelete(tempZip);
            TryDelete(tempSig);
            TryDeleteDirectory(workDir);
            return null;
        }
    }

    private static bool IsStagingFailure(Exception exception) => exception
        is IOException
        or UnauthorizedAccessException
        or InvalidDataException
        or NotSupportedException
        or CryptographicException;

    /// <summary>
    /// Lädt die losgelöste Signatur und prüft sie gegen das heruntergeladene Paket.
    /// Liefert nur <see langword="true"/>, wenn eine Signatur-URL vorliegt, sie
    /// geladen werden konnte und die Signatur unter dem eingebetteten Schlüssel zur
    /// Datei passt. Die Signaturdatei wird anschließend entfernt.
    /// </summary>
    private async Task<bool> VerifyPackageSignatureAsync(UpdateInfo info, string packagePath, string signaturePath, CancellationToken cancellationToken)
    {
        if (info.SignatureUrl is null
            || !await _httpClient.DownloadFileAsync(info.SignatureUrl, signaturePath, cancellationToken).ConfigureAwait(false))
        {
            UpdateInstallerLog.SignatureMissing(_logger, info.LatestVersion);
            TryDelete(signaturePath);
            return false;
        }

        byte[] signature = await File.ReadAllBytesAsync(signaturePath, cancellationToken).ConfigureAwait(false);
        TryDelete(signaturePath);

        if (!_signatureVerifier.VerifyFile(packagePath, signature))
        {
            UpdateInstallerLog.SignatureInvalid(_logger, info.LatestVersion);
            return false;
        }

        UpdateInstallerLog.SignatureVerified(_logger, info.LatestVersion);
        return true;
    }

    /// <summary>
    /// Liefert den Staging-Ordner eines ausstehenden Updates, aber nur wenn er zur
    /// vermerkten Version gehört, neuer als die aktuelle Version ist und der Hash
    /// der entpackten Programmdatei mit dem vermerkten übereinstimmt. Schützt
    /// davor, einen untergeschobenen Ordner einzuspielen.
    /// </summary>
    /// <param name="current">Aktuell laufende Version.</param>
    /// <param name="expectedVersion">Vermerkte Version des ausstehenden Updates.</param>
    /// <param name="expectedSha256">Vermerkter SHA-256 der Programmdatei.</param>
    /// <returns>Staging-Ordner, oder <c>null</c>, wenn keiner sicher passt.</returns>
    public string? FindVerifiedPendingUpdateDirectory(Version current, string? expectedVersion, string? expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(current);

        // Nur ein Update einspielen, das diese Installation selbst vermerkt hat
        // (Version + Datei-Hash). Untergeschobene Ordner ohne passenden Eintrag
        // werden ignoriert.
        if (string.IsNullOrWhiteSpace(expectedVersion)
            || string.IsNullOrWhiteSpace(expectedSha256)
            || !Version.TryParse(expectedVersion, out Version? version)
            || version <= current)
        {
            return null;
        }

        try
        {
            string dir = Path.Combine(_stagingRoot, expectedVersion);
            string exe = ExecutablePathIn(dir);
            if (!File.Exists(exe))
            {
                return null;
            }

            string actual = ComputeFileHash(exe);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                UpdateInstallerLog.HashMismatch(_logger, dir);
                return null;
            }

            return dir;
        }
        catch (IOException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return null;
        }
    }

    /// <summary>Berechnet den SHA-256 (Hex, Kleinbuchstaben) einer Datei.</summary>
    /// <param name="path">Dateipfad.</param>
    /// <returns>Hex-Hash.</returns>
    public static string ComputeFileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Kopiert die Dateien eines Staging-Ordners über das Zielverzeichnis. Die
    /// Portable-Markierung wird ausgelassen, damit eine installierte Version nicht
    /// versehentlich in den Portable-Modus wechselt; vorhandene Benutzerdaten
    /// bleiben erhalten (es wird nur überschrieben, nie gelöscht).
    /// </summary>
    /// <param name="sourceDir">Staging-Ordner mit den neuen Dateien.</param>
    /// <param name="targetDir">Zielverzeichnis (Programmordner).</param>
    public void ApplyStagedFiles(string sourceDir, string targetDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDir);

        // Überschriebene Dateien werden zuvor gesichert, damit bei einem Fehler
        // mitten im Tausch der vorige (lauffähige) Stand wiederhergestellt werden
        // kann — ein halb eingespieltes Update darf die Installation nicht zerstören.
        List<string> backups = new();
        try
        {
            foreach (string sourcePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, sourcePath);

                // Portable-Markierung nie übernehmen — sie entscheidet über den Datenort.
                if (string.Equals(relative, AppPaths.PortableFlagFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string targetPath = Path.Combine(targetDir, relative);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                if (File.Exists(targetPath))
                {
                    string backupPath = targetPath + BackupSuffix;
                    File.Copy(targetPath, backupPath, overwrite: true);
                    backups.Add(targetPath);
                }

                CopyWithRetry(sourcePath, targetPath);
            }

            // Erfolg: Sicherungen entfernen.
            foreach (string targetPath in backups)
            {
                TryDelete(targetPath + BackupSuffix);
            }

            UpdateInstallerLog.Applied(_logger, sourceDir, targetDir);
        }
        catch
        {
            RollBack(backups);
            throw;
        }
    }

    private void RollBack(List<string> backups)
    {
        foreach (string targetPath in backups)
        {
            string backupPath = targetPath + BackupSuffix;
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, targetPath, overwrite: true);
                    File.Delete(backupPath);
                }
            }
            catch (IOException ex)
            {
                UpdateInstallerLog.RollbackFailed(_logger, ex, targetPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                UpdateInstallerLog.RollbackFailed(_logger, ex, targetPath);
            }
        }
    }

    /// <summary>Entfernt Staging-Ordner, die nicht neuer als die aktuelle Version sind.</summary>
    /// <param name="current">Aktuell laufende Version.</param>
    public void CleanObsolete(Version current)
    {
        ArgumentNullException.ThrowIfNull(current);

        try
        {
            if (!Directory.Exists(_stagingRoot))
            {
                return;
            }

            foreach (string dir in Directory.EnumerateDirectories(_stagingRoot))
            {
                if (!Version.TryParse(Path.GetFileName(dir), out Version? version) || version <= current)
                {
                    TryDeleteDirectory(dir);
                }
            }
        }
        catch (IOException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
        }
    }

    /// <summary>Prüft, ob in das Verzeichnis geschrieben werden darf (Probe-Datei).</summary>
    /// <param name="directory">Zu prüfendes Verzeichnis.</param>
    /// <returns><c>true</c>, wenn beschreibbar.</returns>
    public static bool IsDirectoryWritable(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        string probe = Path.Combine(directory, ".lookaway-write-probe.tmp");
        try
        {
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        // Jeder Datei- oder Rechtefehler bedeutet schlicht: nicht beschreibbar.
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private void CopyWithRetry(string sourcePath, string targetPath)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < CopyRetries)
            {
                // Datei evtl. noch vom beendenden Prozess gesperrt — kurz warten und erneut versuchen.
                UpdateInstallerLog.CopyRetry(_logger, targetPath, attempt);
                Thread.Sleep(CopyRetryDelay);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Aufräumen ist best-effort: eine gesperrte Datei wird beim nächsten Lauf entfernt.
        }
        catch (UnauthorizedAccessException)
        {
            // dito
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist best-effort: ein gesperrter Ordner wird beim nächsten Lauf entfernt.
        }
        catch (UnauthorizedAccessException)
        {
            // dito
        }
    }
}

/// <summary>Source-generierte Logging-Methoden des Update-Installers.</summary>
internal static partial class UpdateInstallerLog
{
    [LoggerMessage(EventId = 1650, Level = LogLevel.Information, Message = "Update {Version} entpackt nach {Directory}.")]
    public static partial void Staged(ILogger logger, string version, string directory);

    [LoggerMessage(EventId = 1651, Level = LogLevel.Warning, Message = "Update {Version} konnte nicht heruntergeladen/entpackt werden.")]
    public static partial void StageFailed(ILogger logger, Exception exception, string version);

    [LoggerMessage(EventId = 1652, Level = LogLevel.Warning, Message = "Entpacktes Paket in {Directory} ist unvollständig (keine EXE).")]
    public static partial void PackageInvalid(ILogger logger, string directory);

    [LoggerMessage(EventId = 1653, Level = LogLevel.Warning, Message = "Staging-Ordner konnte nicht durchsucht werden.")]
    public static partial void ScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1654, Level = LogLevel.Information, Message = "Update-Dateien von {Source} nach {Target} kopiert.")]
    public static partial void Applied(ILogger logger, string source, string target);

    [LoggerMessage(EventId = 1655, Level = LogLevel.Debug, Message = "Kopieren von {Target} erneut versucht (Versuch {Attempt}).")]
    public static partial void CopyRetry(ILogger logger, string target, int attempt);

    [LoggerMessage(EventId = 1656, Level = LogLevel.Error, Message = "Rückrollen von {Target} nach fehlgeschlagenem Update misslungen.")]
    public static partial void RollbackFailed(ILogger logger, Exception exception, string target);

    [LoggerMessage(EventId = 1657, Level = LogLevel.Warning, Message = "Ausstehendes Update in {Directory} abgelehnt: Datei-Hash stimmt nicht mit dem vermerkten überein.")]
    public static partial void HashMismatch(ILogger logger, string directory);

    [LoggerMessage(EventId = 1661, Level = LogLevel.Error, Message = "Update-Helfer abgelehnt: Quellordner {Source} liegt außerhalb des Staging-Bereichs.")]
    public static partial void UntrustedSource(ILogger logger, string source);

    [LoggerMessage(EventId = 1662, Level = LogLevel.Error, Message = "Update-Helfer abgelehnt: Zielordner {Target} ist keine beschreibbare Installation.")]
    public static partial void UntrustedTarget(ILogger logger, string target);

    [LoggerMessage(EventId = 1658, Level = LogLevel.Warning, Message = "Update {Version} abgelehnt: keine losgelöste Signatur vorhanden oder ladbar.")]
    public static partial void SignatureMissing(ILogger logger, string version);

    [LoggerMessage(EventId = 1659, Level = LogLevel.Error, Message = "Update {Version} abgelehnt: Signatur passt nicht zum Paket (eingebetteter Schlüssel).")]
    public static partial void SignatureInvalid(ILogger logger, string version);

    [LoggerMessage(EventId = 1660, Level = LogLevel.Information, Message = "Update {Version}: Paket-Signatur erfolgreich geprüft.")]
    public static partial void SignatureVerified(ILogger logger, string version);
}
