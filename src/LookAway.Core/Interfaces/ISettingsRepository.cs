using LookAway.Core.Entities;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Persistenz-Vertrag fuer die Benutzerkonfiguration.
/// </summary>
/// <remarks>
/// Implementierungen muessen atomar schreiben (kein halbgeschriebenes File auf
/// Crash) und beim Lesen einer fehlenden oder beschaedigten Quelle Defaults
/// zurueckliefern, ohne eine Exception zu werfen.
/// </remarks>
public interface ISettingsRepository
{
    /// <summary>
    /// Liest die persistierten Einstellungen.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>
    /// Die gelesenen Einstellungen, oder neue Default-Einstellungen mit
    /// <see cref="Settings.IsFirstRun"/> = <c>true</c>, wenn keine Datei
    /// vorhanden oder die Datei nicht deserialisiert werden konnte.
    /// </returns>
    Task<Settings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Schreibt die Einstellungen atomar.
    /// </summary>
    /// <param name="settings">Zu speichernde Einstellungen.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    Task SaveAsync(Settings settings, CancellationToken cancellationToken = default);
}
