namespace LookAway.Application.ViewModels;

/// <summary>
/// Ein einzelner Balken der Statistik-Visualisierung: Beschriftung, Anzahl und
/// die auf den groessten Wert normierte Hoehe in Pixeln.
/// </summary>
/// <param name="Label">Bezeichnung (Wochentag oder Monat).</param>
/// <param name="Count">Anzahl der Erinnerungen.</param>
/// <param name="BarHeight">Hoehe des Balkens in geraetenunabhaengigen Pixeln.</param>
public sealed record StatBarViewModel(string Label, int Count, double BarHeight);
