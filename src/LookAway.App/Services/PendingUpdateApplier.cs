using System.ComponentModel;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Spielt ein bereitgestelltes Update ein: beim Start den Helfer für ein vermerktes, verifiziertes
/// Paket anstoßen und im Helfer selbst die Dateien ersetzen und die Anwendung neu starten.
/// </summary>
internal sealed class PendingUpdateApplier
{
    private readonly UpdateInstallerService _installer;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IUpdateProcess _process;
    private readonly Version _appVersion;
    private readonly ILogger _logger;

    /// <summary>Erzeugt den Einspieler.</summary>
    /// <param name="installer">Prüft Ordner und tauscht Dateien.</param>
    /// <param name="settingsRepository">Ablage des Vermerks.</param>
    /// <param name="process">Startet Helfer und Anwendung.</param>
    /// <param name="appVersion">Laufende Version.</param>
    /// <param name="logger">Protokolliert Ablehnungen und Fehler.</param>
    public PendingUpdateApplier(
        UpdateInstallerService installer,
        ISettingsRepository settingsRepository,
        IUpdateProcess process,
        Version appVersion,
        ILogger logger)
    {
        _installer = installer;
        _settingsRepository = settingsRepository;
        _process = process;
        _appVersion = appVersion;
        _logger = logger;
    }

    /// <summary>
    /// Prüft beim Start auf ein bereits entpacktes, verifiziertes Paket und startet den
    /// Helfer-Prozess für den Datei-Tausch.
    /// </summary>
    /// <returns><c>true</c>, wenn ein Update eingespielt wird und die App sich dafür beenden soll.</returns>
    public async Task<bool> TryApplyOnStartupAsync()
    {
        try
        {
            // Nur ein Update einspielen, das diese Installation selbst vermerkt hat
            // (Version + Datei-Hash) — nie einen einfach untergeschobenen Ordner.
            Settings settings = await _settingsRepository.LoadAsync().ConfigureAwait(true);
            string? staged = _installer.FindVerifiedPendingUpdateDirectory(
                _appVersion, settings.PendingUpdateVersion, settings.PendingUpdateSha256);
            if (staged is null)
            {
                await DiscardRecordAsync(settings).ConfigureAwait(true);
                return false;
            }

            return StartHelper(staged);
        }
        // Ein fehlgeschlagenes Update darf den Start nie verhindern: protokollieren
        // und regulär weiterstarten.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            return false;
        }
    }

    /// <summary>
    /// Helfer-Modus: wartet auf das Ende der alten Instanz, verifiziert die Quelle erneut
    /// (Staging-Bereich + Datei-Hash), ersetzt die Dateien im übergebenen Programmverzeichnis und
    /// startet die neue Version. Beendet danach den Prozess.
    /// </summary>
    /// <param name="apply">Geparste Helfer-Argumente.</param>
    /// <returns>Der Helfer-Ablauf; er endet mit dem Beenden des Prozesses.</returns>
    public async Task RunHelperAsync(UpdateApplyArgs apply)
    {
        // Das Ziel kommt aus den Argumenten: Der Helfer läuft aus dem Staging-Ordner,
        // sein eigenes Programmverzeichnis ist also nicht die Installation. Vertraut
        // wird dem Pfad trotzdem nicht — er muss eine vorhandene, beschreibbare
        // Installation außerhalb des Staging-Bereichs sein.
        try
        {
            await ReplaceFilesIfTrustedAsync(apply).ConfigureAwait(false);
        }
        // Bei Fehler hat ApplyStagedFilesAsync auf den vorigen Stand zurückgerollt.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
        }
        finally
        {
            // In jedem Fall die installierte App starten (neuer Stand bei Erfolg,
            // zurückgerollter, lauffähiger Stand bei Fehler), dann den Helfer beenden.
            TryStartInstalledApp(apply.Target);
            _process.Exit();
        }
    }

    // Zu diesem Vermerk gehört kein einspielbarer Staging-Ordner (mehr): Er ist entweder erledigt
    // — der häufigste Fall, denn nach erfolgreichem Einspielen läuft genau diese Version — oder
    // unbrauchbar. Stehen bleiben darf er nicht, sonst wird bei jedem Start ein längst
    // eingespieltes Update erneut gesucht. Geschrieben wird nur, wenn tatsächlich einer gesetzt ist.
    private async Task DiscardRecordAsync(Settings settings)
    {
        _installer.CleanObsolete(_appVersion);

        if (!string.IsNullOrWhiteSpace(settings.PendingUpdateVersion) || !string.IsNullOrWhiteSpace(settings.PendingUpdateSha256))
        {
            await SettingsWriter.TryChangeAsync(
                _settingsRepository,
                s => (s.PendingUpdateVersion, s.PendingUpdateSha256) = ((string?)null, (string?)null),
                _logger).ConfigureAwait(true);
        }
    }

    private bool StartHelper(string staged)
    {
        string target = _process.InstallDirectory;
        if (!UpdateInstallerService.IsDirectoryWritable(target))
        {
            // z. B. „für alle Benutzer"-Installation in Programme — kein Auto-Tausch.
            UpdateOrchestratorLog.TargetNotWritable(_logger, target);
            return false;
        }

        UpdateOrchestratorLog.ApplyingPendingUpdate(_logger, staged);
        _process.StartApply(
            UpdateInstallerService.ExecutablePathIn(staged),
            new UpdateApplyArgs(_process.CurrentProcessId, staged, target));
        return true;
    }

    // Die Signaturprüfung des Hauptprozesses hier erneut durchsetzen: Der Helfer vertraut seinen
    // Argumenten nicht, sondern verifiziert, dass der Quellordner im Staging-Bereich liegt und die
    // Programmdatei den zuvor signaturgeprüft vermerkten Hash trägt.
    private async Task ReplaceFilesIfTrustedAsync(UpdateApplyArgs apply)
    {
        Settings settings = await _settingsRepository.LoadAsync().ConfigureAwait(false);
        if (!_installer.IsTrustedStagingDirectory(apply.Source, settings.PendingUpdateSha256)
            || !_installer.IsTrustedTargetDirectory(apply.Target))
        {
            UpdateOrchestratorLog.ApplyRejected(_logger, apply.Source);
            return;
        }

        // Die alte Instanz beendet sich gleich nach dem Start des Helfers. Dreißig
        // Sekunden fangen auch einen langsamen Abbau ab; danach wird trotzdem
        // kopiert, und der Datei-Tausch wartet auf noch gesperrte Dateien.
        _process.WaitForExit(apply.Pid, TimeSpan.FromSeconds(30));
        await _installer.ApplyStagedFilesAsync(apply.Source, apply.Target).ConfigureAwait(false);
    }

    private void TryStartInstalledApp(string targetDir)
    {
        try
        {
            _process.StartApp(UpdateInstallerService.ExecutablePathIn(targetDir));
        }
        // Der Helfer beendet sich anschließend ohnehin; ein fehlgeschlagener Neustart
        // wird nur protokolliert.
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
        }
    }
}
