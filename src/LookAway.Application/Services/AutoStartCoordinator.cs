using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Haelt die Autostart-Einstellung (<see cref="Settings.AutoStart"/>) und den
/// Windows-Registry-Eintrag synchron. Kapselt die beiden Synchronisations-
/// Richtungen: Benutzeraenderung -> Registry und Startup-Abgleich Registry ->
/// Einstellungen.
/// </summary>
/// <remarks>
/// Die Klasse ist die testbare "Settings-Logik" aus BRIEF004: sie kennt nur die
/// Core-Interfaces <see cref="IAutoStartService"/> und
/// <see cref="ISettingsRepository"/> und laesst sich mit Fakes ohne echte
/// Registry pruefen.
/// </remarks>
public sealed class AutoStartCoordinator
{
    private readonly IAutoStartService _autoStartService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<AutoStartCoordinator> _logger;

    /// <summary>
    /// Erzeugt den Koordinator mit Autostart-Dienst, Settings-Persistenz und Logger.
    /// </summary>
    /// <param name="autoStartService">Plattform-Dienst fuer den Autostart-Eintrag.</param>
    /// <param name="settingsRepository">Persistenz der Benutzereinstellungen.</param>
    /// <param name="logger">Logger fuer Synchronisations-Vorgaenge.</param>
    public AutoStartCoordinator(
        IAutoStartService autoStartService,
        ISettingsRepository settingsRepository,
        ILogger<AutoStartCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(autoStartService);
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _autoStartService = autoStartService;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Wendet eine Benutzeraenderung an: aktualisiert zuerst die Registry und
    /// persistiert anschliessend die Einstellung. Schlaegt die Registry-
    /// Operation fehl, wird nichts gespeichert (kein inkonsistenter Zustand).
    /// </summary>
    /// <param name="enabled">Gewuenschter Autostart-Zustand.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <exception cref="LookAway.Core.Exceptions.AutoStartException">
    /// Der Registry-Eintrag konnte nicht aktualisiert werden.
    /// </exception>
    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        ApplyToRegistry(enabled);

        Settings settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (settings.AutoStart != enabled)
        {
            settings.AutoStart = enabled;
            await _settingsRepository.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        AutoStartCoordinatorLog.SettingApplied(_logger, enabled);
    }

    /// <summary>
    /// Gleicht beim App-Start die Einstellung an die Registry an. Die Registry
    /// ist fuer den An/Aus-Zustand die fuehrende Quelle, damit ein manueller
    /// Eingriff (z. B. Deaktivieren ueber den Task-Manager) uebernommen wird.
    /// Ist Autostart aktiv, wird zusaetzlich der hinterlegte Pfad auf den
    /// aktuellen Stand gebracht (Anwendung wurde verschoben).
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Der nach dem Abgleich gueltige Autostart-Zustand.</returns>
    /// <exception cref="LookAway.Core.Exceptions.AutoStartException">
    /// Der Registry-Status konnte nicht gelesen oder korrigiert werden.
    /// </exception>
    public async Task<bool> SynchronizeFromRegistryAsync(CancellationToken cancellationToken = default)
    {
        Settings settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        bool registryEnabled = _autoStartService.IsEnabled();

        if (registryEnabled != settings.AutoStart)
        {
            settings.AutoStart = registryEnabled;
            await _settingsRepository.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            AutoStartCoordinatorLog.SettingsAlignedToRegistry(_logger, registryEnabled);
        }

        if (registryEnabled)
        {
            // Pfadkorrektur: idempotent, schreibt nur bei veraltetem Eintrag.
            _autoStartService.Enable();
        }

        return registryEnabled;
    }

    private void ApplyToRegistry(bool enabled)
    {
        if (enabled)
        {
            _autoStartService.Enable();
        }
        else
        {
            _autoStartService.Disable();
        }
    }
}
