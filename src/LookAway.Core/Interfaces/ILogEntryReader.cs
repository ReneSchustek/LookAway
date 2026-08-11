using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Liefert die zuletzt geschriebenen Einträge des Anwendungsprotokolls.
/// </summary>
public interface ILogEntryReader
{
    /// <summary>
    /// Liest die jüngsten Protokolleinträge, neueste zuerst.
    /// </summary>
    /// <param name="maxEntries">Obergrenze der gelieferten Einträge.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>
    /// Die gefundenen Einträge; eine leere Liste, wenn noch nichts protokolliert
    /// wurde oder das Protokoll nicht lesbar ist. Ein unlesbares Protokoll ist kein
    /// Fehler, den der Benutzer beheben könnte — die Ansicht zeigt dann den
    /// Leerzustand.
    /// </returns>
    Task<IReadOnlyList<LogEntry>> ReadRecentAsync(int maxEntries, CancellationToken cancellationToken = default);
}
