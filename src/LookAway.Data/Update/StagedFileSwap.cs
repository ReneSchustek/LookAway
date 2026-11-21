using LookAway.Core.Domain;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Update;

/// <summary>
/// Kopiert die Dateien eines Staging-Ordners über das Programmverzeichnis. Überschriebene Dateien
/// werden vorher gesichert; scheitert der Tausch mittendrin, wird der vorige, lauffähige Stand
/// wiederhergestellt — ein halb eingespieltes Update darf die Installation nicht zerstören.
/// </summary>
internal sealed class StagedFileSwap
{
    private const string BackupSuffix = ".bak-update";

    // Zehn Versuche mit dem eingestellten Abstand (Vorgabe 500 ms, also bis zu viereinhalb
    // Sekunden), damit die gerade beendete Vorgänger-Instanz ihre Programmdateien freigibt und
    // ein Virenscanner die neue Datei fertig geprüft hat.
    private const int CopyRetries = 10;

    private readonly ILogger _logger;
    private readonly TimeSpan _copyRetryDelay;

    /// <summary>Erzeugt den Datei-Tausch.</summary>
    /// <param name="logger">Protokolliert Wiederholungen, Erfolg und misslungenes Rückrollen.</param>
    /// <param name="copyRetryDelay">Wartezeit zwischen zwei Kopierversuchen.</param>
    public StagedFileSwap(ILogger logger, TimeSpan copyRetryDelay)
    {
        _logger = logger;
        _copyRetryDelay = copyRetryDelay;
    }

    /// <summary>
    /// Kopiert alle Dateien außer der Portable-Markierung über das Ziel; vorhandene Benutzerdaten
    /// bleiben erhalten (es wird nur überschrieben, nie gelöscht).
    /// </summary>
    /// <param name="sourceDir">Staging-Ordner mit den neuen Dateien.</param>
    /// <param name="targetDir">Zielverzeichnis (Programmordner).</param>
    /// <param name="cancellationToken">Abbruch; ein Abbruch rollt wie jeder Fehler zurück.</param>
    /// <returns>Der Tausch; bei einem Fehler nach dem Rückrollen die Ausnahme.</returns>
    public async Task ApplyAsync(string sourceDir, string targetDir, CancellationToken cancellationToken)
    {
        List<string> backups = [];
        try
        {
            foreach (string sourcePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                await ReplaceFileAsync(sourceDir, targetDir, sourcePath, backups, cancellationToken).ConfigureAwait(false);
            }

            // Erfolg: Sicherungen entfernen.
            foreach (string targetPath in backups)
            {
                UpdateFiles.TryDelete(targetPath + BackupSuffix);
            }

            UpdateInstallerLog.Applied(_logger, sourceDir, targetDir);
        }
        catch
        {
            // Jeder Fehler, auch ein unerwarteter, rollt zurück; der Aufrufer protokolliert ihn.
            RollBack(backups);
            throw;
        }
    }

    // Ersetzt eine Datei, nachdem eine vorhandene gesichert wurde. Die Portable-Markierung wird nie
    // übernommen — sie entscheidet über den Datenort, eine installierte Version wechselte sonst
    // versehentlich in den Portable-Modus.
    private async Task ReplaceFileAsync(string sourceDir, string targetDir, string sourcePath, List<string> backups, CancellationToken cancellationToken)
    {
        string relative = Path.GetRelativePath(sourceDir, sourcePath);
        if (string.Equals(relative, AppPaths.PortableFlagFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string targetPath = Path.Combine(targetDir, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!); // ein voller Dateipfad unter dem Ziel hat immer einen Ordner

        if (File.Exists(targetPath))
        {
            File.Copy(targetPath, targetPath + BackupSuffix, overwrite: true);
            backups.Add(targetPath);
        }

        await CopyWithRetryAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);
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
            catch (Exception ex) when (UpdateFiles.IsFileAccessFailure(ex))
            {
                UpdateInstallerLog.RollbackFailed(_logger, ex, targetPath);
            }
        }
    }

    private async Task CopyWithRetryAsync(string sourcePath, string targetPath, CancellationToken cancellationToken)
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
                await Task.Delay(_copyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
