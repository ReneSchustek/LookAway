namespace LookAway.Core.ValueObjects;

/// <summary>
/// Aggregierte Statistik eines Zeitraums (Heute / Woche / Jahr).
/// </summary>
/// <param name="Count">Gesamtzahl der Erinnerungen.</param>
/// <param name="Skipped">Anzahl übersprungener Erinnerungen.</param>
/// <param name="TotalBreakTime">Summe der gemachten Pausenzeit.</param>
/// <param name="Buckets">Feingliederung (leer bei „Heute").</param>
public sealed record PeriodStatistics(
    int Count,
    int Skipped,
    TimeSpan TotalBreakTime,
    IReadOnlyList<StatBucket> Buckets);
