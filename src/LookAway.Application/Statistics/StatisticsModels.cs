namespace LookAway.Application.Statistics;

/// <summary>
/// Aggregierte Kennzahl eines Zeitabschnitts (z. B. ein Wochentag oder Monat).
/// </summary>
/// <param name="Label">Anzeigebezeichnung des Abschnitts.</param>
/// <param name="Count">Anzahl der Erinnerungen im Abschnitt.</param>
/// <param name="TotalBreakTime">Summe der tatsächlich gemachten Pausenzeit.</param>
public sealed record StatBucket(string Label, int Count, TimeSpan TotalBreakTime);

/// <summary>
/// Aggregierte Statistik eines Zeitraums (Heute / Woche / Jahr).
/// </summary>
/// <param name="Count">Gesamtzahl der Erinnerungen.</param>
/// <param name="Skipped">Anzahl übersprungener Erinnerungen.</param>
/// <param name="TotalBreakTime">Summe der gemachten Pausenzeit.</param>
/// <param name="Buckets">Feingliederung (leer bei "Heute").</param>
public sealed record PeriodStatistics(
    int Count,
    int Skipped,
    TimeSpan TotalBreakTime,
    IReadOnlyList<StatBucket> Buckets);
