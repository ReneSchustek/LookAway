using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Prueft, ob eine neuere LookAway-Version verfuegbar ist.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Prueft auf ein Update. Netzwerkfehler fuehren nicht zu einer Exception,
    /// sondern zu einem Ergebnis mit <see cref="UpdateInfo.IsUpdateAvailable"/> = <c>false</c>.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Das Ergebnis der Pruefung.</returns>
    Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
