using System.ComponentModel;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Kapselt den gesamten Aktualisierungs-Ablauf: Hintergrund-Prüfung beim Start,
/// die vom Nutzer ausgelöste Ein-Klick-Installation, das automatische Staging, das
/// verifizierte Einspielen beim nächsten Start und den Helfer-Modus, der die Dateien
/// nach dem Beenden der alten Instanz ersetzt. Der Ablauf steht hier und nicht in der
/// App-Klasse, damit diese nur den Lebenszyklus verantwortet.
/// </summary>
/// <remarks>
/// Die Prüfung beim Start steht in <see cref="StartupUpdateCheck"/>, das Einspielen in
/// <see cref="PendingUpdateApplier"/>. Hier bleiben die gefundene Version und die
/// Ein-Klick-Installation aus dem Infobereich.
/// </remarks>
internal sealed class UpdateOrchestrator
{
    private readonly UpdateInstallerService _installer;
    private readonly IUpdateProcess _process;
    private readonly ILogger<UpdateOrchestrator> _logger;
    private readonly SetupUpdate _setupUpdate;
    private readonly StartupUpdateCheck _startupCheck;
    private readonly PendingUpdateApplier _applier;

    private Uri? _downloadUrl;
    private UpdateInfo? _pending;

    /// <summary>Erzeugt den Orchestrator mit seinen Abhängigkeiten.</summary>
    /// <param name="installer">Lädt, prüft und tauscht Update-Pakete.</param>
    /// <param name="checker">Fragt den Release-Dienst.</param>
    /// <param name="settingsRepository">Ablage für Prüfzeitpunkt und Vermerk.</param>
    /// <param name="clock">Uhr für die Fälligkeit der Prüfung.</param>
    /// <param name="process">Startet Helfer, Anwendung und Browser.</param>
    /// <param name="appVersion">Laufende Version.</param>
    /// <param name="logger">Protokolliert den Ablauf.</param>
    public UpdateOrchestrator(
        UpdateInstallerService installer,
        IUpdateChecker checker,
        ISettingsRepository settingsRepository,
        IClock clock,
        IUpdateProcess process,
        Version appVersion,
        ILogger<UpdateOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentNullException.ThrowIfNull(logger);

        _installer = installer;
        _process = process;
        _logger = logger;
        _setupUpdate = new SetupUpdate(installer, process, logger);
        _startupCheck = new StartupUpdateCheck(installer, _setupUpdate, checker, settingsRepository, clock, appVersion, logger);
        _applier = new PendingUpdateApplier(installer, settingsRepository, process, appVersion, logger);
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
    /// <returns>Der Vorgang.</returns>
    public async Task CheckAtStartupAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        UpdateInfo? info = await _startupCheck.CheckAsync(settings, cancellationToken).ConfigureAwait(true);
        if (info is null)
        {
            return;
        }

        _pending = info;
        _downloadUrl = info.DownloadUrl;
        UpdateAvailableChanged?.Invoke(true);

        await _startupCheck.StageIfWantedAsync(info, settings, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Tray-Aktion „Update": Eine Installation startet das geprüfte Setup (Windows fragt nach
    /// Administratorrechten, das Setup beendet und startet LookAway). Eine portable Kopie lädt das
    /// Paket, entpackt es und spielt es über den Helfer-Prozess sofort ein. Scheitert beides, wird
    /// stattdessen die Release-Seite geöffnet.
    /// </summary>
    public void HandleManualUpdateRequested() => _ = HandleManualUpdateRequestedAsync();

    /// <summary>Wie <see cref="HandleManualUpdateRequested"/>, aber abwartbar.</summary>
    /// <returns>Der Vorgang bis zum Start des Helfers oder bis zum Rückfall auf die Release-Seite.</returns>
    public async Task HandleManualUpdateRequestedAsync()
    {
        if (_setupUpdate.IsActive)
        {
            await StartSetupOrOpenReleasePageAsync().ConfigureAwait(true);
            return;
        }

        await ApplyPackageNowAsync().ConfigureAwait(true);
    }

    // Installation: das geprüfte Setup starten; es beendet LookAway selbst.
    private async Task StartSetupOrOpenReleasePageAsync()
    {
        if (_pending is null || !await _setupUpdate.StartAsync(_pending).ConfigureAwait(true))
        {
            OpenReleasePage();
        }
    }

    // Portable Kopie: Paket laden und über den Helfer sofort einspielen.
    private async Task ApplyPackageNowAsync()
    {
        try
        {
            string target = _process.InstallDirectory;
            StagedUpdate? staged = _pending?.PackageUrl is not null && UpdateInstallerService.IsDirectoryWritable(target)
                ? await _installer.DownloadAndStageAsync(_pending).ConfigureAwait(true)
                : null;
            if (staged is null)
            {
                OpenReleasePage();
                return;
            }

            // Vom Nutzer angestoßen und gerade frisch geladen -> direkt einspielen.
            _process.StartApply(
                UpdateInstallerService.ExecutablePathIn(staged.Directory),
                new UpdateApplyArgs(_process.CurrentProcessId, staged.Directory, target));
            RelaunchRequested?.Invoke();
        }
        // Datei- und Prozessfehler beim Bereitstellen: auf die Release-Seite zurückfallen.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            UpdateOrchestratorLog.ApplyFailed(_logger, ex);
            OpenReleasePage();
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
    public Task<bool> TryApplyPendingUpdateOnStartupAsync() => _applier.TryApplyOnStartupAsync();

    /// <summary>
    /// Helfer-Modus: wartet auf das Ende der alten Instanz, verifiziert die Quelle
    /// erneut (Staging-Bereich + Datei-Hash), ersetzt die Dateien im übergebenen
    /// Programmverzeichnis und startet die neue Version. Beendet danach den Prozess.
    /// </summary>
    /// <param name="apply">Geparste Helfer-Argumente.</param>
    /// <returns>Der Helfer-Ablauf; er endet mit dem Beenden des Prozesses.</returns>
    public Task RunHelperApplyAsync(UpdateApplyArgs apply) => Task.Run(() => _applier.RunHelperAsync(apply));

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
            _process.OpenInBrowser(_downloadUrl);
        }
        // Fehlt ein Standardbrowser oder verweigert die Shell den Start, meldet
        // Process.Start genau diese Typen; die App läuft unbeeindruckt weiter.
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            UpdateOrchestratorLog.BrowserOpenFailed(_logger, ex);
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
        EventId = 1162,
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
