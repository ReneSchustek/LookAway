using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using LookAway.Core.Entities;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Kapselt den gesamten Aktualisierungs-Ablauf: Hintergrund-Prüfung beim Start,
/// die vom Nutzer ausgelöste Ein-Klick-Installation, das automatische Staging, das
/// verifizierte Einspielen beim nächsten Start und den Helfer-Modus, der die Dateien
/// nach dem Beenden der alten Instanz ersetzt. Ausgelagert aus der App-Klasse, damit
/// diese nur noch den Lebenszyklus verantwortet.
/// </summary>
internal sealed class UpdateOrchestrator
{
    private readonly UpdateInstallerService _installer;
    private readonly IUpdateChecker _checker;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IClock _clock;
    private readonly Version _appVersion;
    private readonly ILogger<UpdateOrchestrator> _logger;

    private Uri? _downloadUrl;
    private UpdateInfo? _pending;

    /// <summary>Erzeugt den Orchestrator mit seinen Abhängigkeiten.</summary>
    public UpdateOrchestrator(
        UpdateInstallerService installer,
        IUpdateChecker checker,
        ISettingsRepository settingsRepository,
        IClock clock,
        Version appVersion,
        ILogger<UpdateOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentNullException.ThrowIfNull(logger);

        _installer = installer;
        _checker = checker;
        _settingsRepository = settingsRepository;
        _clock = clock;
        _appVersion = appVersion;
        _logger = logger;
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich der „Update verfügbar"-Zustand ändert (für die
    /// Tray-Anzeige). Von der App verdrahtet.
    /// </summary>
    public Action<bool>? UpdateAvailableChanged { get; set; }

    /// <summary>
    /// Wird aufgerufen, wenn ein vom Nutzer angestoßenes Update bereitsteht und die
    /// App sich für den Helfer-Prozess beenden soll. Von der App verdrahtet.
    /// </summary>
    public Action? RelaunchRequested { get; set; }

    /// <summary>
    /// Prüft im Hintergrund auf eine neuere Version (falls fällig), vermerkt den
    /// Prüfzeitpunkt und stellt bei aktivierter Auto-Aktualisierung das Paket im
    /// Hintergrund bereit (Staging).
    /// </summary>
    /// <param name="settings">Aktuelle Konfiguration.</param>
    /// <param name="cancellationToken">
    /// Wird beim Beenden der Anwendung ausgelöst. Ohne ihn liefe die Prüfung weiter,
    /// während der Container bereits abgebaut wird — der Vermerk des Prüfzeitpunkts
    /// träfe dann auf eine entsorgte Ablage, und zwar in einem Vorgang, den niemand
    /// mehr beobachtet.
    /// </param>
    public async Task CheckAtStartupAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.UpdateCheckEnabled)
        {
            return;
        }

        DateTimeOffset now = _clock.UtcNow;
        if (!UpdateSchedule.IsDue(settings.UpdateCheckFrequency, settings.LastUpdateCheck, now))
        {
            return;
        }

        UpdateInfo info;
        try
        {
            info = await _checker.CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // Routinemäßiger Offline-Start darf nicht als unbeobachteter Fehler enden.
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await PersistLastCheckAsync(now, cancellationToken).ConfigureAwait(true);

        if (!info.IsUpdateAvailable)
        {
            return;
        }

        _pending = info;
        _downloadUrl = info.DownloadUrl;
        UpdateAvailableChanged?.Invoke(true);

        // Auto-Update: Paket im Hintergrund herunterladen, entpacken und Version +
        // Datei-Hash vermerken; das eigentliche Einspielen erfolgt beim nächsten Start
        // nur für genau dieses (verifizierte) Paket.
        if (settings.AutoUpdate && info.PackageUrl is not null && !IsAlreadyStaged(info, settings))
        {
            await AutoStageAsync(info, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Tray-Aktion „Update": lädt die gefundene Aktualisierung herunter, entpackt sie
    /// und spielt sie über den Helfer-Prozess sofort ein (die App startet danach neu).
    /// Bei fehlendem Paket oder schreibgeschütztem Programmordner wird stattdessen die
    /// Release-Seite geöffnet.
    /// </summary>
    public void HandleManualUpdateRequested() => _ = HandleManualUpdateRequestedAsync();

    private async Task HandleManualUpdateRequestedAsync()
    {
        try
        {
            UpdateInfo? info = _pending;
            string target = AppContext.BaseDirectory;

            if (info?.PackageUrl is null || !UpdateInstallerService.IsDirectoryWritable(target))
            {
                OpenReleasePage();
                return;
            }

            StagedUpdate? staged = await _installer.DownloadAndStageAsync(info).ConfigureAwait(true);
            if (staged is null)
            {
                OpenReleasePage();
                return;
            }

            // Vom Nutzer angestoßen und gerade frisch geladen -> direkt einspielen.
            UpdateProcess.StartApply(
                UpdateInstallerService.ExecutablePathIn(staged.Directory),
                new UpdateApplyArgs(Environment.ProcessId, staged.Directory, target));
            RelaunchRequested?.Invoke();
        }
        // Datei- und Prozessfehler beim Bereitstellen: auf die Release-Seite zurückfallen.
        catch (IOException ex)
        {
            OnManualUpdateFailed(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            OnManualUpdateFailed(ex);
        }
        catch (Win32Exception ex)
        {
            OnManualUpdateFailed(ex);
        }
        catch (InvalidOperationException ex)
        {
            OnManualUpdateFailed(ex);
        }
    }

    /// <summary>
    /// Prüft beim Start auf ein bereits entpacktes, verifiziertes Paket und startet
    /// den Helfer-Prozess für den Datei-Tausch.
    /// </summary>
    /// <returns>
    /// <c>true</c>, wenn ein Update eingespielt wird und die App sich dafür beenden
    /// soll; sonst <c>false</c> (regulär weiterstarten).
    /// </returns>
    public async Task<bool> TryApplyPendingUpdateOnStartupAsync()
    {
        try
        {
            string target = AppContext.BaseDirectory;

            // Nur ein Update einspielen, das diese Installation selbst vermerkt hat
            // (Version + Datei-Hash) — nie einen einfach untergeschobenen Ordner.
            Settings settings = await _settingsRepository.LoadAsync().ConfigureAwait(true);

            string? staged = _installer.FindVerifiedPendingUpdateDirectory(
                _appVersion, settings.PendingUpdateVersion, settings.PendingUpdateSha256);
            if (staged is null)
            {
                _installer.CleanObsolete(_appVersion);

                // Zu diesem Vermerk gehört kein einspielbarer Staging-Ordner (mehr): Er ist
                // entweder erledigt — der häufigste Fall, denn nach erfolgreichem Einspielen
                // läuft genau diese Version — oder unbrauchbar. Stehen bleiben darf er nicht,
                // sonst wird bei jedem Start ein längst eingespieltes Update erneut gesucht.
                await ClearPendingUpdateAsync(settings).ConfigureAwait(true);
                return false;
            }

            if (!UpdateInstallerService.IsDirectoryWritable(target))
            {
                // z. B. „für alle Benutzer"-Installation in Programme — kein Auto-Tausch.
                UpdateOrchestratorLog.TargetNotWritable(_logger, target);
                return false;
            }

            UpdateOrchestratorLog.ApplyingPendingUpdate(_logger, staged);
            UpdateProcess.StartApply(
                UpdateInstallerService.ExecutablePathIn(staged),
                new UpdateApplyArgs(Environment.ProcessId, staged, target));
            return true;
        }
        // Ein fehlgeschlagenes Update darf den Start nie verhindern: protokollieren
        // und regulär weiterstarten.
        catch (IOException ex)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            return false;
        }
        catch (Win32Exception ex)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            return false;
        }
    }

    /// <summary>
    /// Helfer-Modus: wartet auf das Ende der alten Instanz, verifiziert die Quelle
    /// erneut (Staging-Bereich + Datei-Hash), ersetzt die Dateien im eigenen
    /// Programmverzeichnis und startet die neue Version. Beendet danach den Prozess.
    /// </summary>
    /// <param name="apply">Geparste Helfer-Argumente.</param>
    public void RunHelperApply(UpdateApplyArgs apply)
    {
        _ = Task.Run(async () =>
        {
            // Das Ziel kommt aus den Argumenten: Der Helfer läuft aus dem Staging-Ordner,
            // sein eigenes Programmverzeichnis ist also nicht die Installation. Vertraut
            // wird dem Pfad trotzdem nicht — er muss eine vorhandene, beschreibbare
            // Installation außerhalb des Staging-Bereichs sein.
            string target = apply.Target;
            try
            {
                // Die Signaturprüfung des Hauptprozesses hier erneut durchsetzen: Der
                // Helfer vertraut seinen Argumenten nicht, sondern verifiziert, dass der
                // Quellordner im Staging-Bereich liegt und die Programmdatei den zuvor
                // signaturgeprüft vermerkten Hash trägt.
                Settings settings = await _settingsRepository.LoadAsync().ConfigureAwait(false);
                if (!_installer.IsTrustedStagingDirectory(apply.Source, settings.PendingUpdateSha256)
                    || !_installer.IsTrustedTargetDirectory(target))
                {
                    UpdateOrchestratorLog.ApplyRejected(_logger, apply.Source);
                    return;
                }

                WaitForProcessExit(apply.Pid, TimeSpan.FromSeconds(30));
                _installer.ApplyStagedFiles(apply.Source, target);
            }
            // Bei Fehler hat ApplyStagedFiles auf den vorigen Stand zurückgerollt.
            catch (IOException ex)
            {
                UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            }
            catch (InvalidOperationException ex)
            {
                UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            }
            finally
            {
                // In jedem Fall die installierte App starten (neuer Stand bei Erfolg,
                // zurückgerollter, lauffähiger Stand bei Fehler), dann den Helfer beenden.
                TryStartInstalledApp(target);
                Environment.Exit(0);
            }
        });
    }

    /// <summary>
    /// Löscht den Vermerk eines ausstehenden Updates. Schreibt nur, wenn tatsächlich
    /// einer gesetzt ist, damit ein regulärer Start die Einstellungen nicht anfasst.
    /// </summary>
    private async Task ClearPendingUpdateAsync(Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.PendingUpdateVersion)
            && string.IsNullOrWhiteSpace(settings.PendingUpdateSha256))
        {
            return;
        }

        try
        {
            Settings current = await _settingsRepository.LoadAsync().ConfigureAwait(true);
            current.PendingUpdateVersion = null;
            current.PendingUpdateSha256 = null;
            await _settingsRepository.SaveAsync(current).ConfigureAwait(true);
        }
        // Ein misslungenes Aufräumen darf den Start nie verhindern — der Vermerk bleibt
        // dann eben stehen und wird beim nächsten Start erneut verworfen.
        catch (UnauthorizedAccessException ex)
        {
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
        }
        catch (IOException ex)
        {
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
        }
    }

    private async Task PersistLastCheckAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            Settings current = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(true);
            current.LastUpdateCheck = now;
            await _settingsRepository.SaveAsync(current, cancellationToken).ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
        }
        catch (IOException ex)
        {
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
        }
    }

    // Lädt/entpackt das Update im Hintergrund und vermerkt Version + Datei-Hash in den
    // Einstellungen, damit es beim nächsten Start verifiziert eingespielt wird.
    private async Task AutoStageAsync(UpdateInfo info, CancellationToken cancellationToken)
    {
        StagedUpdate? staged = await _installer.DownloadAndStageAsync(info, cancellationToken).ConfigureAwait(true);
        if (staged is null)
        {
            return;
        }

        try
        {
            Settings settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(true);
            settings.PendingUpdateVersion = staged.Version;
            settings.PendingUpdateSha256 = staged.ExecutableSha256;
            await _settingsRepository.SaveAsync(settings, cancellationToken).ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
        }
        catch (IOException ex)
        {
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Liegt für genau diese Aktualisierung bereits ein verifiziertes Paket im
    /// Staging-Ordner? Dann nicht erneut laden. Sonst würde die entpackte EXE bei jedem
    /// Start neu geschrieben und vom Virenscanner erneut geprüft — solange dieser Scan
    /// das Einspielen (kurzzeitig „Zugriff verweigert") blockiert, käme das Update nie
    /// durch. Ein einmal gestagetes Paket „kühlt ab" und wird beim nächsten Start
    /// eingespielt.
    /// </summary>
    private bool IsAlreadyStaged(UpdateInfo info, Settings settings)
    {
        if (!string.Equals(settings.PendingUpdateVersion, info.LatestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _installer.FindVerifiedPendingUpdateDirectory(
            _appVersion, settings.PendingUpdateVersion, settings.PendingUpdateSha256) is not null;
    }

    /// <remarks>
    /// Die Adresse stammt aus der Antwort des Release-Dienstes, also von außen.
    /// <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> nimmt beim Auswerten jedes
    /// absolute Schema an — auch <c>file:</c> oder ein Anwendungsprotokoll. Mit
    /// <c>UseShellExecute</c> startete dann nicht der Browser, sondern was auch immer
    /// das System für dieses Schema hinterlegt hat. Dieselbe Prüfung schützt bereits die
    /// Verweise der Über-Seite; sie gehört auch hierher.
    /// </remarks>
    private void OpenReleasePage()
    {
        if (!SafeLinkLauncher.IsAllowed(_downloadUrl))
        {
            UpdateOrchestratorLog.ReleasePageRejected(_logger, _downloadUrl?.ToString() ?? string.Empty);
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo(_downloadUrl.ToString()) { UseShellExecute = true });
        }
        // Fehlt ein Standardbrowser oder verweigert die Shell den Start, meldet
        // Process.Start genau diese Typen; die App läuft unbeeindruckt weiter.
        catch (Win32Exception ex)
        {
            UpdateOrchestratorLog.BrowserOpenFailed(_logger, ex);
        }
        catch (InvalidOperationException ex)
        {
            UpdateOrchestratorLog.BrowserOpenFailed(_logger, ex);
        }
    }

    private void OnManualUpdateFailed(Exception exception)
    {
        UpdateOrchestratorLog.ApplyFailed(_logger, exception);
        OpenReleasePage();
    }

    private void TryStartInstalledApp(string targetDir)
    {
        try
        {
            UpdateProcess.StartApp(UpdateInstallerService.ExecutablePathIn(targetDir));
        }
        // Der Helfer beendet sich anschließend ohnehin; ein fehlgeschlagener Neustart
        // wird nur protokolliert.
        catch (Win32Exception ex)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
        }
        catch (InvalidOperationException ex)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
        }
    }

    private static void WaitForProcessExit(int pid, TimeSpan timeout)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            _ = process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // Prozess existiert nicht mehr — bereits beendet.
        }
        catch (InvalidOperationException)
        {
            // Prozess bereits beendet.
        }
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Update-Orchestrators.
/// </summary>
internal static partial class UpdateOrchestratorLog
{
    [LoggerMessage(EventId = 1160, Level = LogLevel.Warning, Message = "Update-Prüfung konnte nicht abgeschlossen werden.")]
    public static partial void CheckPersistFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1161, Level = LogLevel.Warning, Message = "Die Update-Seite konnte nicht im Browser geöffnet werden.")]
    public static partial void BrowserOpenFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1171,
        Level = LogLevel.Warning,
        Message = "Adresse der Release-Seite abgelehnt — nur http und https werden geöffnet: {Address}")]
    public static partial void ReleasePageRejected(ILogger logger, string address);

    [LoggerMessage(EventId = 1170, Level = LogLevel.Information, Message = "Ausstehendes Update wird eingespielt: {Directory}.")]
    public static partial void ApplyingPendingUpdate(ILogger logger, string directory);

    [LoggerMessage(EventId = 1171, Level = LogLevel.Warning, Message = "Programmordner {Directory} ist nicht beschreibbar — automatische Aktualisierung nicht möglich.")]
    public static partial void TargetNotWritable(ILogger logger, string directory);

    [LoggerMessage(EventId = 1172, Level = LogLevel.Warning, Message = "Automatische Aktualisierung fehlgeschlagen.")]
    public static partial void ApplyFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1173, Level = LogLevel.Error, Message = "Update-Helfer abgelehnt: Quelle {Source} nicht vertrauenswürdig (Signatur/Hash-Prüfung).")]
    public static partial void ApplyRejected(ILogger logger, string source);
}
