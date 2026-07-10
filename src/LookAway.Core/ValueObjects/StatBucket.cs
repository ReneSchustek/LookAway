namespace LookAway.Core.ValueObjects;

/// <summary>
/// Aggregierte Kennzahl eines Zeitabschnitts (z. B. ein Wochentag oder Monat).
/// </summary>
/// <param name="Label">Anzeigebezeichnung des Abschnitts.</param>
/// <param name="Count">Anzahl der Erinnerungen im Abschnitt.</param>
/// <param name="TotalBreakTime">Summe der tatsächlich gemachten Pausenzeit.</param>
public sealed record StatBucket(string Label, int Count, TimeSpan TotalBreakTime);
