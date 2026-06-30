using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Ergebnis eines erfolgreichen Downloads/Entpackens: der Staging-Ordner, die
/// Version und der SHA-256 der entpackten Programmdatei. Der Hash wird in den
/// Einstellungen vermerkt und vor dem Einspielen erneut geprueft.
/// </summary>
/// <param name="Directory">Staging-Ordner mit den neuen Dateien.</param>
/// <param name="Version">Versionszeichenkette des Updates.</param>
/// <param name="ExecutableSha256">SHA-256 (Hex) der entpackten <c>LookAway.exe</c>.</param>
public sealed record StagedUpdate(string Directory, string Version, string ExecutableSha256);

/// <summary>
/// Verwaltet die automatische Aktualisierung ueber die Portable-ZIP eines
/// GitHub-Release: laedt das Paket herunter, entpackt es in einen Staging-Ordner
/// und ersetzt — beim naechsten Start, ausgeloest vom Anwendungs-Bootstrap — die
/// Programmdateien. Die reine Datei-/Versionslogik ist hier gekapselt; das
/// Beenden/Neustarten der Prozesse steuert die App-Schicht.
/// </summary>
public sealed class UpdateInstallerService
{
    private const string ExecutableName = "LookAway.exe";
    private const string BackupSuffix = ".bak-update";
    private const int CopyRetries = 10;
    private const long MaxExtractedBytes = 1024L * 1024 * 1024;
    private const int MaxEntries = 20_000;
    private static readonly TimeSpan CopyRetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly IHttpGetClient _httpClient;
    private readonly ILogger<UpdateInstallerService> _logger;
    private readonly string _stagingRoot;

    /// <summary>Erzeugt den Dienst.</summary>
    /// <param name="httpClient">HTTP-Zugriff fuer den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    public UpdateInstallerService(IHttpGetClient httpClient, ILogger<UpdateInstallerService> logger)
        : this(httpClient, logger, DefaultStagingRoot())
    {
    }

    /// <summary>Konstruktor mit explizitem Staging-Wurzelpfad (fuer Tests).</summary>
    /// <param name="httpClient">HTTP-Zugriff fuer den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="stagingRoot">Wurzelordner fuer entpackte Update-Pakete.</param>
    public UpdateInstallerService(IHttpGetClient httpClient, ILogger<UpdateInstallerService> logger, string stagingRoot)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);

        _httpClient = httpClient;
        _logger = logger;
        _stagingRoot = stagingRoot;
    }

    /// <summary>Wurzelordner, unter dem Update-Pakete je Version entpackt werden.</summary>
    public string StagingRoot => _stagingRoot;

    /// <summary>Pfad der ausfuehrbaren Datei innerhalb eines Staging-Ordners.</summary>
    public static string ExecutablePathIn(string directory) => Path.Combine(directory, ExecutableName);

    private static string DefaultStagingRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppPaths.AppFolderName,
            "updates");

    /// <summary>
    /// Laedt das Portable-Paket der angegebenen Aktualisierung herunter und
    /// entpackt es in einen versionsbenannten Staging-Ordner.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung (mit <see cref="UpdateInfo.PackageUrl"/>).</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Staging-Ordner, Version und Datei-Hash bei Erfolg, sonst <c>null</c>.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Die Aktualisierung ist unkritisch: Download-/Entpackfehler werden geloggt und als Misserfolg behandelt, damit die laufende App nie abstuerzt.")]
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
        string workDir = Path.Combine(_stagingRoot, $"_work-{token}");

        try
        {
            _ = Directory.CreateDirectory(_stagingRoot);

            if (!await _httpClient.DownloadFileAsync(info.PackageUrl, tempZip, cancellationToken).ConfigureAwait(false))
            {
                // DownloadFileAsync meldet Misserfolg ohne zu werfen — die evtl.
                // angelegte Teildatei hier selbst aufraeumen (catch greift nicht).
                TryDelete(tempZip);
                return null;
            }

            await Task.Run(() => ExtractSafely(tempZip, workDir), cancellationToken).ConfigureAwait(false);
            File.Delete(tempZip);

            if (!File.Exists(ExecutablePathIn(workDir)))
            {
                UpdateInstallerLog.PackageInvalid(_logger, workDir);
                TryDeleteDirectory(workDir);
                return null;
            }

            // Atomar an den endgueltigen, versionsbenannten Ort versetzen.
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }

            Directory.Move(workDir, stagingDir);

            string sha = ComputeFileHash(ExecutablePathIn(stagingDir));
            UpdateInstallerLog.Staged(_logger, info.LatestVersion, stagingDir);
            return new StagedUpdate(stagingDir, info.LatestVersion, sha);
        }
        catch (Exception ex)
        {
            UpdateInstallerLog.StageFailed(_logger, ex, info.LatestVersion);
            TryDelete(tempZip);
            TryDeleteDirectory(workDir);
            return null;
        }
    }

    /// <summary>
    /// Entpackt ein ZIP sicher: erzwingt eine Obergrenze fuer Eintragszahl und
    /// entpackte Gesamtgroesse (Schutz vor Zip-Bomben) und weist Eintraege ab, die
    /// das Zielverzeichnis verlassen wuerden (Zip-Slip).
    /// </summary>
    private static void ExtractSafely(string zipPath, string destinationDir)
    {
        _ = Directory.CreateDirectory(destinationDir);
        string destFull = Path.GetFullPath(destinationDir) + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        if (archive.Entries.Count > MaxEntries)
        {
            throw new InvalidOperationException($"ZIP enthaelt zu viele Eintraege ({archive.Entries.Count}).");
        }

        long totalWritten = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string targetPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
            if (!targetPath.StartsWith(destFull, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsicherer ZIP-Eintrag (Pfadverlassen): {entry.FullName}");
            }

            // Verzeichniseintrag (endet auf '/').
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                _ = Directory.CreateDirectory(targetPath);
                continue;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            // Auf tatsaechlich geschriebene Bytes begrenzen statt auf die im ZIP
            // deklarierte (manipulierbare) Groesse zu vertrauen — Schutz vor Zip-Bomben.
            using Stream source = entry.Open();
            using FileStream destination = File.Create(targetPath);
            byte[] buffer = new byte[81920];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalWritten += read;
                if (totalWritten > MaxExtractedBytes)
                {
                    throw new InvalidOperationException("Entpackte Gesamtgroesse ueberschreitet das Limit.");
                }

                destination.Write(buffer, 0, read);
            }
        }
    }

    /// <summary>
    /// Liefert den Staging-Ordner eines ausstehenden Updates, aber nur wenn er zur
    /// vermerkten Version gehoert, neuer als die aktuelle Version ist und der Hash
    /// der entpackten Programmdatei mit dem vermerkten uebereinstimmt. Schuetzt
    /// davor, einen untergeschobenen Ordner einzuspielen.
    /// </summary>
    /// <param name="current">Aktuell laufende Version.</param>
    /// <param name="expectedVersion">Vermerkte Version des ausstehenden Updates.</param>
    /// <param name="expectedSha256">Vermerkter SHA-256 der Programmdatei.</param>
    /// <returns>Staging-Ordner, oder <c>null</c>, wenn keiner sicher passt.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Das Suchen ist unkritisch: jeder Datei-/Zugriffsfehler wird geloggt und als 'kein Update' behandelt.")]
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
        catch (Exception ex)
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
    /// Kopiert die Dateien eines Staging-Ordners ueber das Zielverzeichnis. Die
    /// Portable-Markierung wird ausgelassen, damit eine installierte Version nicht
    /// versehentlich in den Portable-Modus wechselt; vorhandene Benutzerdaten
    /// bleiben erhalten (es wird nur ueberschrieben, nie geloescht).
    /// </summary>
    /// <param name="sourceDir">Staging-Ordner mit den neuen Dateien.</param>
    /// <param name="targetDir">Zielverzeichnis (Programmordner).</param>
    public void ApplyStagedFiles(string sourceDir, string targetDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDir);

        // Ueberschriebene Dateien werden zuvor gesichert, damit bei einem Fehler
        // mitten im Tausch der vorige (lauffaehige) Stand wiederhergestellt werden
        // kann — ein halb eingespieltes Update darf die Installation nicht zerstoeren.
        List<string> backups = new();
        try
        {
            foreach (string sourcePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, sourcePath);

                // Portable-Markierung nie uebernehmen — sie entscheidet ueber den Datenort.
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
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Aufraeumen ist unkritisch: Fehler werden geloggt und ignoriert.")]
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
        catch (Exception ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
        }
    }

    /// <summary>Prueft, ob in das Verzeichnis geschrieben werden darf (Probe-Datei).</summary>
    /// <param name="directory">Zu pruefendes Verzeichnis.</param>
    /// <returns><c>true</c>, wenn beschreibbar.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Die Schreibprobe darf nicht werfen; jeder Fehler bedeutet schlicht 'nicht beschreibbar'.")]
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
        catch (Exception)
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

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort-Aufraeumen; Fehler sind unkritisch.")]
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // bewusst ignoriert
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort-Aufraeumen; Fehler sind unkritisch.")]
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
            // bewusst ignoriert
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

    [LoggerMessage(EventId = 1652, Level = LogLevel.Warning, Message = "Entpacktes Paket in {Directory} ist unvollstaendig (keine EXE).")]
    public static partial void PackageInvalid(ILogger logger, string directory);

    [LoggerMessage(EventId = 1653, Level = LogLevel.Warning, Message = "Staging-Ordner konnte nicht durchsucht werden.")]
    public static partial void ScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1654, Level = LogLevel.Information, Message = "Update-Dateien von {Source} nach {Target} kopiert.")]
    public static partial void Applied(ILogger logger, string source, string target);

    [LoggerMessage(EventId = 1655, Level = LogLevel.Debug, Message = "Kopieren von {Target} erneut versucht (Versuch {Attempt}).")]
    public static partial void CopyRetry(ILogger logger, string target, int attempt);

    [LoggerMessage(EventId = 1656, Level = LogLevel.Error, Message = "Rueckrollen von {Target} nach fehlgeschlagenem Update misslungen.")]
    public static partial void RollbackFailed(ILogger logger, Exception exception, string target);

    [LoggerMessage(EventId = 1657, Level = LogLevel.Warning, Message = "Ausstehendes Update in {Directory} abgelehnt: Datei-Hash stimmt nicht mit dem vermerkten ueberein.")]
    public static partial void HashMismatch(ILogger logger, string directory);
}
