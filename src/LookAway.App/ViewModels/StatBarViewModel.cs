namespace LookAway.App.ViewModels;

/// <summary>
/// Ein einzelner Balken der Statistik-Visualisierung: Beschriftung, Anzahl und
/// die auf den größten Wert normierte Höhe in Pixeln.
/// </summary>
/// <param name="Label">Bezeichnung (Wochentag oder Monat).</param>
/// <param name="Count">Anzahl der Erinnerungen.</param>
/// <param name="BarHeight">Höhe des Balkens in gerätenunabhängigen Pixeln.</param>
internal sealed record StatBarViewModel(string Label, int Count, double BarHeight);
