using Microsoft.Extensions.Logging;

namespace LookAway.Data.Update;

/// <summary>
/// Entscheidet, welchem Ordner ein Update vertrauen darf: Quellordner nur im Staging-Bereich und
/// mit dem vermerkten Hash, Zielordner nur als echte, beschreibbare Installation außerhalb davon.
/// Räumt außerdem veraltete Staging-Ordner ab.
/// </summary>
internal sealed class StagingTrust
{
    private readonly string _stagingRoot;
    private readonly ILogger _logger;

    /// <summary>Erzeugt die Prüfung für einen Staging-Wurzelordner.</summary>
    /// <param name="stagingRoot">Wurzelordner, unter dem Update-Pakete je Version liegen.</param>
    /// <param name="logger">Protokolliert Ablehnungen und Lesefehler.</param>
    public StagingTrust(string stagingRoot, ILogger logger)
    {
        _stagingRoot = stagingRoot;
        _logger = logger;
    }

    /// <summary>
    /// Ob der Quellordner im Staging-Bereich liegt und seine Programmdatei den erwarteten Hash
    /// trägt. So wird die Signaturprüfung des Hauptprozesses auch im Helfer durchgesetzt.
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
            string sourceFull = FullPath(sourceDir);

            // Der Quellpfad muss echt unterhalb des Staging-Wurzelordners liegen.
            if (!sourceFull.StartsWith(RootFull() + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                UpdateInstallerLog.UntrustedSource(_logger, sourceFull);
                return false;
            }

            return HasExpectedExecutable(sourceFull, expectedSha256);
        }
        catch (Exception ex) when (IsUntrustablePath(ex))
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return false;
        }
    }

    /// <summary>
    /// Ob der Zielordner eine echte Installation ist, die ersetzt werden darf: vorhanden, ohne
    /// erhöhte Rechte beschreibbar, mit Programmdatei und außerhalb des Staging-Bereichs (sonst
    /// wäre die Quelle ihr eigenes Ziel).
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
            string rootFull = RootFull();
            string targetFull = FullPath(targetDir);
            bool insideStaging = targetFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase)
                || targetFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (insideStaging || !File.Exists(UpdateFiles.ExecutablePathIn(targetFull)) || !UpdateFiles.IsDirectoryWritable(targetFull))
            {
                UpdateInstallerLog.UntrustedTarget(_logger, targetFull);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (IsUntrustablePath(ex))
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return false;
        }
    }

    /// <summary>
    /// Der Staging-Ordner eines ausstehenden Updates, aber nur, wenn er zur vermerkten Version
    /// gehört, neuer als die laufende ist und den vermerkten Hash trägt.
    /// </summary>
    /// <param name="current">Aktuell laufende Version.</param>
    /// <param name="expectedVersion">Vermerkte Version des ausstehenden Updates.</param>
    /// <param name="expectedSha256">Vermerkter SHA-256 der Programmdatei.</param>
    /// <returns>Staging-Ordner, oder <c>null</c>, wenn keiner sicher passt.</returns>
    public string? FindVerifiedPendingUpdateDirectory(Version current, string? expectedVersion, string? expectedSha256)
    {
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
            return HasExpectedExecutable(dir, expectedSha256) ? dir : null;
        }
        catch (Exception ex) when (UpdateFiles.IsFileAccessFailure(ex))
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
            return null;
        }
    }

    /// <summary>Entfernt Staging-Ordner, die nicht neuer als die aktuelle Version sind.</summary>
    /// <param name="current">Aktuell laufende Version.</param>
    public void CleanObsolete(Version current)
    {
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
                    UpdateFiles.TryDeleteDirectory(dir);
                }
            }
        }
        catch (Exception ex) when (UpdateFiles.IsFileAccessFailure(ex))
        {
            UpdateInstallerLog.ScanFailed(_logger, ex);
        }
    }

    // Die Pfade der Vertrauensprüfung kommen von der Befehlszeile des Helfers. Ein Pfad, den
    // Path.GetFullPath gar nicht erst annimmt (etwa mit Nullzeichen), ist nicht vertrauenswürdig.
    private static bool IsUntrustablePath(Exception exception)
        => exception is ArgumentException || UpdateFiles.IsFileAccessFailure(exception);

    private static string FullPath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private string RootFull() => FullPath(_stagingRoot);

    // Programmdatei vorhanden und mit dem vermerkten Hash; ein abweichender Hash wird protokolliert.
    private bool HasExpectedExecutable(string directory, string expectedSha256)
    {
        string exe = UpdateFiles.ExecutablePathIn(directory);
        if (!File.Exists(exe))
        {
            return false;
        }

        if (!string.Equals(UpdateFiles.ComputeFileHash(exe), expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            UpdateInstallerLog.HashMismatch(_logger, directory);
            return false;
        }

        return true;
    }
}
