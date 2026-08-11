namespace LookAway.App.ViewModels;

/// <summary>
/// Die eigenständigen Bereiche des Einstellungsfensters.
/// </summary>
/// <param name="Statistics">Statistik über die aufgezeichneten Pausen.</param>
/// <param name="BreakModels">Pausenmodelle mit Suche und Filter.</param>
/// <param name="Log">Anwendungsprotokoll mit Suche und Filter.</param>
/// <param name="Tasks">Aufgaben des aufgabenbasierten Modells.</param>
/// <remarks>
/// Jeder Bereich bringt seine eigene Datenquelle und seinen eigenen Zustand mit und
/// steht für sich. Sie hier zu bündeln hält den Konstruktor des
/// <see cref="SettingsViewModel"/> lesbar: Vorher standen vier Bereiche als vier
/// weitere Parameter zwischen den Diensten, und mit jedem neuen Bereich wäre ein
/// weiterer dazugekommen.
/// </remarks>
internal sealed record SettingsSections(
    StatisticsViewModel Statistics,
    BreakModelListViewModel BreakModels,
    LogViewModel Log,
    WorkTaskListViewModel Tasks);
