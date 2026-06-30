using LookAway.Core.Entities;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Persistenz-Vertrag für die Pausen-Historie. Append-only mit
/// automatischem Aufräumen alter Einträge.
/// </summary>
public interface IBreakHistoryRepository
{
    /// <summary>
    /// Hängt eine Sitzung an die Historie an.
    /// </summary>
    /// <param name="session">Aufzuzeichnende Sitzung.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    Task AppendAsync(BreakSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Liest alle gespeicherten Sitzungen (älteste zuerst).
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Alle Sitzungen; leere Liste bei fehlender oder beschädigter Datei.</returns>
    Task<IReadOnlyList<BreakSession>> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Entfernt Sitzungen, die vor dem Stichtag begonnen haben.
    /// </summary>
    /// <param name="cutoff">Stichtag; ältere Einträge werden gelöscht.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Anzahl der entfernten Einträge.</returns>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
