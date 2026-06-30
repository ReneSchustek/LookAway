using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Prüft, ob eine neuere LookAway-Version verfügbar ist.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Prüft auf ein Update. Netzwerkfehler führen nicht zu einer Exception,
    /// sondern zu einem Ergebnis mit <see cref="UpdateInfo.IsUpdateAvailable"/> = <c>false</c>.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Das Ergebnis der Prüfung.</returns>
    Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
