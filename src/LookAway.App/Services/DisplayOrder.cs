
namespace LookAway.App.Services;

/// <summary>
/// Reihenfolge, in der die Overlay-Fenster auf den Monitoren erscheinen.
/// </summary>
internal static class DisplayOrder
{
    /// <summary>
    /// Nebenmonitore zuerst, der Hauptmonitor zuletzt; ohne <paramref name="allScreens"/> nur der
    /// Hauptmonitor.
    /// </summary>
    /// <typeparam name="T">Monitor-Typ, etwa <c>DisplayArea</c>.</typeparam>
    /// <param name="primary">Hauptmonitor.</param>
    /// <param name="all">Alle gefundenen Monitore, der Hauptmonitor eingeschlossen.</param>
    /// <param name="idOf">Liefert die Kennung eines Monitors.</param>
    /// <param name="allScreens">Alle Monitore abdunkeln.</param>
    /// <returns>Die Monitore in Anzeigereihenfolge.</returns>
    /// <remarks>
    /// Jedes Fenster wird beim Anzeigen aktiviert und nimmt dem vorigen dabei den Fokus — nach dem
    /// letzten liegt er also auf dem Hauptmonitor, wo der Inhalt steht und Esc wirkt. Gezählt wird
    /// über den Index, weil die WinRT-Liste der Monitore beim Aufzählen mit foreach wirft.
    /// </remarks>
    public static IReadOnlyList<T> Arrange<T>(T primary, IReadOnlyList<T> all, Func<T, ulong> idOf, bool allScreens)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(idOf);

        if (!allScreens)
        {
            return [primary];
        }

        ulong primaryId = idOf(primary);
        List<T> ordered = new(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            if (idOf(all[i]) != primaryId)
            {
                ordered.Add(all[i]);
            }
        }

        ordered.Add(primary);
        return ordered;
    }
}
