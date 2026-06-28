using LookAway.Core.Entities;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Persistenz-Vertrag fuer die Pausen-Historie (BRIEF018). Append-only mit
/// automatischem Aufraeumen alter Eintraege.
/// </summary>
public interface IBreakHistoryRepository
{
    /// <summary>
    /// Haengt eine Sitzung an die Historie an.
    /// </summary>
    /// <param name="session">Aufzuzeichnende Sitzung.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    Task AppendAsync(BreakSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Liest alle gespeicherten Sitzungen (aelteste zuerst).
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Alle Sitzungen; leere Liste bei fehlender oder beschaedigter Datei.</returns>
    Task<IReadOnlyList<BreakSession>> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Entfernt Sitzungen, die vor dem Stichtag begonnen haben.
    /// </summary>
    /// <param name="cutoff">Stichtag; aeltere Eintraege werden geloescht.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Anzahl der entfernten Eintraege.</returns>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
