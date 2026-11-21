using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Die Update-Prüfung beim Start: fragt, wenn sie fällig ist, nach einer neueren Version, vermerkt
/// den Prüfzeitpunkt und stellt bei automatischer Aktualisierung das Paket im Hintergrund bereit.
/// </summary>
internal sealed class StartupUpdateCheck
{
    private readonly UpdateInstallerService _installer;
    private readonly ISetupUpdate _setupUpdate;
    private readonly IUpdateChecker _checker;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IClock _clock;
    private readonly Version _appVersion;
    private readonly ILogger _logger;

    /// <summary>Erzeugt die Prüfung.</summary>
    /// <param name="installer">Lädt und prüft das Paket.</param>
    /// <param name="setupUpdate">Bereitet bei einer Installation das Setup vor.</param>
    /// <param name="checker">Fragt den Release-Dienst.</param>
    /// <param name="settingsRepository">Ablage für Prüfzeitpunkt und Vermerk.</param>
    /// <param name="clock">Uhr für die Fälligkeit.</param>
    /// <param name="appVersion">Laufende Version.</param>
    /// <param name="logger">Protokolliert Offline-Starts und Schreibfehler.</param>
    public StartupUpdateCheck(
        UpdateInstallerService installer,
        ISetupUpdate setupUpdate,
        IUpdateChecker checker,
        ISettingsRepository settingsRepository,
        IClock clock,
        Version appVersion,
        ILogger logger)
    {
        _installer = installer;
        _setupUpdate = setupUpdate;
        _checker = checker;
        _settingsRepository = settingsRepository;
        _clock = clock;
        _appVersion = appVersion;
        _logger = logger;
    }

    /// <summary>
    /// Prüft, falls eingeschaltet und fällig, und vermerkt den Prüfzeitpunkt.
    /// </summary>
    /// <param name="settings">Aktuelle Konfiguration.</param>
    /// <param name="cancellationToken">Abbruch beim Beenden der Anwendung.</param>
    /// <returns>Die neuere Version, oder <c>null</c>, wenn keine vorliegt oder nicht geprüft wurde.</returns>
    public async Task<UpdateInfo?> CheckAsync(Settings settings, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        if (!settings.UpdateCheckEnabled || !UpdateSchedule.IsDue(settings.UpdateCheckFrequency, settings.LastUpdateCheck, now))
        {
            return null;
        }

        UpdateInfo? info = await TryAskAsync(cancellationToken).ConfigureAwait(true);
        if (info is null)
        {
            return null;
        }

        await SettingsWriter.TryChangeAsync(_settingsRepository, s => s.LastUpdateCheck = now, _logger, cancellationToken).ConfigureAwait(true);
        return info.IsUpdateAvailable ? info : null;
    }

    /// <summary>
    /// Auto-Update: Eine portable Kopie lädt das Paket im Hintergrund, entpackt es und vermerkt
    /// Version und Datei-Hash; eingespielt wird beim nächsten Start nur genau dieses, verifizierte
    /// Paket. Eine Installation lädt und prüft nur das Setup vor; installiert wird es erst auf Klick,
    /// weil Windows dafür nach Administratorrechten fragt.
    /// </summary>
    /// <param name="info">Die gefundene neuere Version.</param>
    /// <param name="settings">Aktuelle Konfiguration.</param>
    /// <param name="cancellationToken">Abbruch beim Beenden der Anwendung.</param>
    /// <returns>Der Vorgang.</returns>
    public async Task StageIfWantedAsync(UpdateInfo info, Settings settings, CancellationToken cancellationToken)
    {
        if (!settings.AutoUpdate)
        {
            return;
        }

        if (_setupUpdate.IsActive)
        {
            _ = await _setupUpdate.PrepareAsync(info, cancellationToken).ConfigureAwait(true);
            return;
        }

        if (info.PackageUrl is null || IsAlreadyStaged(info, settings))
        {
            return;
        }

        StagedUpdate? staged = await _installer.DownloadAndStageAsync(info, cancellationToken).ConfigureAwait(true);
        if (staged is not null)
        {
            await SettingsWriter.TryChangeAsync(
                _settingsRepository,
                s => (s.PendingUpdateVersion, s.PendingUpdateSha256) = (staged.Version, staged.ExecutableSha256),
                _logger,
                cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task<UpdateInfo?> TryAskAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _checker.CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // Routinemäßiger Offline-Start darf nicht als unbeobachteter Fehler enden.
            UpdateOrchestratorLog.CheckPersistFailed(_logger, ex);
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // Liegt für genau diese Aktualisierung bereits ein verifiziertes Paket im Staging-Ordner?
    // Dann nicht erneut laden. Sonst würde die entpackte EXE bei jedem Start neu geschrieben und
    // vom Virenscanner erneut geprüft — solange dieser Scan das Einspielen (kurzzeitig „Zugriff
    // verweigert") blockiert, käme das Update nie durch. Ein einmal gestagetes Paket „kühlt ab"
    // und wird beim nächsten Start eingespielt.
    private bool IsAlreadyStaged(UpdateInfo info, Settings settings)
        => string.Equals(settings.PendingUpdateVersion, info.LatestVersion, StringComparison.OrdinalIgnoreCase)
            && _installer.FindVerifiedPendingUpdateDirectory(_appVersion, settings.PendingUpdateVersion, settings.PendingUpdateSha256) is not null;
}
