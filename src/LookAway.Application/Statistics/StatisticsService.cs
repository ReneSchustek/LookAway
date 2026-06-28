using System.Globalization;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;

namespace LookAway.Application.Statistics;

/// <summary>
/// Aggregiert die Pausen-Historie zu Kennzahlen fuer Heute, diese Woche und
/// dieses Jahr. Reine, UI-freie Logik; arbeitet in lokaler Zeit
/// relativ zur <see cref="IClock"/>-Gegenwart und ist damit testbar.
/// </summary>
public sealed class StatisticsService
{
    private const int DaysPerWeek = 7;
    private const int MonthsPerYear = 12;

    private readonly IBreakHistoryRepository _historyRepository;
    private readonly IClock _clock;

    /// <summary>
    /// Erzeugt den Service.
    /// </summary>
    /// <param name="historyRepository">Quelle der aufgezeichneten Sitzungen.</param>
    /// <param name="clock">Systemzeit fuer die Zeitraum-Grenzen.</param>
    public StatisticsService(IBreakHistoryRepository historyRepository, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(historyRepository);
        ArgumentNullException.ThrowIfNull(clock);
        _historyRepository = historyRepository;
        _clock = clock;
    }

    /// <summary>Liefert die Kennzahlen des heutigen Tages (ohne Feingliederung).</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task<PeriodStatistics> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BreakSession> all = await _historyRepository.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        DateTime today = Now().Date;

        List<BreakSession> ofDay = all.Where(session => LocalDate(session) == today).ToList();
        return Summarize(ofDay, Array.Empty<StatBucket>());
    }

    /// <summary>Liefert die Kennzahlen der laufenden Woche (Mo–So, 7 Abschnitte).</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task<PeriodStatistics> GetThisWeekAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BreakSession> all = await _historyRepository.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        DateTime weekStart = StartOfWeek(Now().Date);
        DateTime weekEnd = weekStart.AddDays(DaysPerWeek);

        // Lokale Datumsberechnung nur einmal pro Sitzung.
        ILookup<DateTime, BreakSession> byDay = all
            .Select(session => (session, date: LocalDate(session)))
            .Where(item => item.date >= weekStart && item.date < weekEnd)
            .ToLookup(item => item.date, item => item.session);

        List<StatBucket> buckets = new(DaysPerWeek);
        List<BreakSession> ofWeek = new();
        for (int offset = 0; offset < DaysPerWeek; offset++)
        {
            DateTime day = weekStart.AddDays(offset);
            List<BreakSession> ofDay = byDay[day].ToList();
            ofWeek.AddRange(ofDay);
            buckets.Add(BuildBucket(day.ToString("ddd", CultureInfo.CurrentCulture), ofDay));
        }

        return Summarize(ofWeek, buckets);
    }

    /// <summary>Liefert die Kennzahlen des laufenden Jahres (12 Monatsabschnitte).</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task<PeriodStatistics> GetThisYearAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BreakSession> all = await _historyRepository.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        int year = Now().Year;

        // Lokale Datumsberechnung nur einmal pro Sitzung, dann nach Monat gruppieren.
        ILookup<int, BreakSession> byMonth = all
            .Select(session => (session, date: LocalDate(session)))
            .Where(item => item.date.Year == year)
            .ToLookup(item => item.date.Month, item => item.session);

        List<StatBucket> buckets = new(MonthsPerYear);
        List<BreakSession> ofYear = new();
        for (int month = 1; month <= MonthsPerYear; month++)
        {
            List<BreakSession> ofMonth = byMonth[month].ToList();
            ofYear.AddRange(ofMonth);
            string label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month);
            buckets.Add(BuildBucket(label, ofMonth));
        }

        return Summarize(ofYear, buckets);
    }

    private DateTime Now() => _clock.UtcNow.ToLocalTime().DateTime;

    private static DateTime LocalDate(BreakSession session) => session.StartedAt.ToLocalTime().Date;

    private static DateTime StartOfWeek(DateTime date)
    {
        int diff = ((int)date.DayOfWeek + 6) % 7; // Montag als Wochenstart
        return date.AddDays(-diff);
    }

    private static StatBucket BuildBucket(string label, List<BreakSession> sessions)
        => new(label, sessions.Count, SumBreakTime(sessions));

    private static PeriodStatistics Summarize(List<BreakSession> sessions, IReadOnlyList<StatBucket> buckets)
        => new(
            sessions.Count,
            sessions.Count(session => session.Outcome == BreakOutcome.Skipped),
            SumBreakTime(sessions),
            buckets);

    private static TimeSpan SumBreakTime(IEnumerable<BreakSession> sessions)
    {
        TimeSpan total = TimeSpan.Zero;
        foreach (BreakSession session in sessions.Where(s => s.Outcome == BreakOutcome.Taken))
        {
            total += session.Duration;
        }

        return total;
    }
}
