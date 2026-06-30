using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

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
    private const int CopyRetries = 10;
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
    /// <returns>Pfad des Staging-Ordners bei Erfolg, sonst <c>null</c>.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Die Aktualisierung ist unkritisch: Download-/Entpackfehler werden geloggt und als Misserfolg behandelt, damit die laufende App nie abstuerzt.")]
    public async Task<string?> DownloadAndStageAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (info.PackageUrl is null)
        {
            return null;
        }

        string stagingDir = Path.Combine(_stagingRoot, info.LatestVersion);
        string tempZip = stagingDir + ".zip";

        try
        {
            _ = Directory.CreateDirectory(_stagingRoot);

            if (!await _httpClient.DownloadFileAsync(info.PackageUrl, tempZip, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }

            await ZipFile.ExtractToDirectoryAsync(tempZip, stagingDir, cancellationToken).ConfigureAwait(false);
            File.Delete(tempZip);

            if (!File.Exists(ExecutablePathIn(stagingDir)))
            {
                UpdateInstallerLog.PackageInvalid(_logger, stagingDir);
                Directory.Delete(stagingDir, recursive: true);
                return null;
            }

            UpdateInstallerLog.Staged(_logger, info.LatestVersion, stagingDir);
            return stagingDir;
        }
        catch (Exception ex)
        {
            UpdateInstallerLog.StageFailed(_logger, ex, info.LatestVersion);
            TryDelete(tempZip);
            return null;
        }
    }

    /// <summary>
    /// Sucht ein bereits entpacktes, neueres Paket als die aktuell laufende Version.
    /// </summary>
    /// <param name="current">Aktuell laufende Version.</param>
    /// <returns>Staging-Ordner der hoechsten neueren Version, oder <c>null</c>.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Das Suchen ist unkritisch: jeder Datei-/Zugriffsfehler wird geloggt und als 'kein Update' behandelt.")]
    public string? FindPendingUpdateDirectory(Version current)
    {
        ArgumentNullException.ThrowIfNull(current);

        try
        {
            if (!Directory.Exists(_stagingRoot))
            {
                return null;
            }

            string? bestDir = null;
            Version? bestVersion = null;

            foreach (string dir in Directory.EnumerateDirectories(_stagingRoot))
            {
                if (!Version.TryParse(Path.GetFileName(dir), out Version? version)
                    || version <= current
                    || !File.Exists(ExecutablePathIn(dir)))
                {
                    continue;
                }

                if (bestVersion is null || version > bestVersion)
                {
                    bestVersion = version;
                    bestDir = dir;
                }
            }

            return bestDir;
        }
        catch (Exception ex)
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return null;
        }
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
            CopyWithRetry(sourcePath, targetPath);
        }

        UpdateInstallerLog.Applied(_logger, sourceDir, targetDir);
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
}
