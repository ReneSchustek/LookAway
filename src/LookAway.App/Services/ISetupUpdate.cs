using LookAway.Core.ValueObjects;

namespace LookAway.App.Services;

/// <summary>
/// Die Aktualisierung einer installierten Kopie über das signierte Setup des Releases.
/// </summary>
/// <remarks>
/// Eine Setup-Installation liegt unter Program Files, den nur ein Administrator beschreiben darf.
/// Statt Dateien selbst zu tauschen, startet LookAway das geprüfte Setup; Windows fragt dabei
/// nach Administratorrechten. Eine portable Kopie aktualisiert sich weiter über das ZIP.
/// </remarks>
internal interface ISetupUpdate
{
    /// <summary>Aktualisiert sich diese Kopie über das Setup (installiert) statt über das ZIP (portabel)?</summary>
    bool IsActive { get; }

    /// <summary>Ob das Release für diese Kopie etwas Installierbares bereithält.</summary>
    /// <param name="info">Die gefundene Aktualisierung.</param>
    /// <returns>Bei einer Installation: Setup samt Signatur; bei einer portablen Kopie: das ZIP.</returns>
    bool CanInstall(UpdateInfo info);

    /// <summary>Lädt und prüft das Setup im Hintergrund vor, ohne es zu starten.</summary>
    /// <param name="info">Die gefundene Aktualisierung.</param>
    /// <param name="cancellationToken">Abbruch beim Beenden der Anwendung.</param>
    /// <returns><c>true</c>, wenn das Setup geprüft bereitliegt.</returns>
    Task<bool> PrepareAsync(UpdateInfo info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Startet das geprüfte Setup. LookAway läuft weiter: Erst wenn der Benutzer der Rechteabfrage
    /// zustimmt, beendet das Setup die laufende Instanz und startet sie nach der Installation neu.
    /// Bricht er ab, bleibt alles, wie es war.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns><c>true</c>, wenn das Setup gestartet wurde.</returns>
    Task<bool> StartAsync(UpdateInfo info, CancellationToken cancellationToken = default);
}
