using LookAway.Core.Services;
using LookAway.Core.ValueObjects;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Tests.Fakes;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für die Statistik-Aggregation und den CSV-Export.
/// </summary>
public sealed class StatisticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    private static BreakSession Session(DateTimeOffset start, TimeSpan duration, BreakOutcome outcome)
        => new(Guid.NewGuid(), start, start + duration, BreakModel.ClassicPomodoro, outcome);

    private static (StatisticsService Service, FakeBreakHistoryRepository History) CreateService()
    {
        FakeBreakHistoryRepository history = new(new[]
        {
            Session(Now, TimeSpan.FromMinutes(5), BreakOutcome.Taken),
            Session(Now.AddHours(1), TimeSpan.Zero, BreakOutcome.Skipped),
            Session(Now.AddDays(-10), TimeSpan.FromMinutes(10), BreakOutcome.Taken),
        });
        FakeClock clock = new(Now);
        return (new StatisticsService(history, clock), history);
    }

    [Fact]
    public async Task GetToday_zählt_nur_die_heutigen_Sitzungen()
    {
        (StatisticsService service, _) = CreateService();

        PeriodStatistics today = await service.GetTodayAsync();

        Assert.Equal(2, today.Count);
        Assert.Equal(1, today.Skipped);
        Assert.Equal(TimeSpan.FromMinutes(5), today.TotalBreakTime);
    }

    [Fact]
    public async Task GetThisWeek_liefert_sieben_Tagesbalken()
    {
        (StatisticsService service, _) = CreateService();

        PeriodStatistics week = await service.GetThisWeekAsync();

        Assert.Equal(7, week.Buckets.Count);
        Assert.Equal(2, week.Count); // die Sitzung vor 10 Tagen liegt außerhalb dieser Woche
        Assert.Equal(2, week.Buckets.Sum(bucket => bucket.Count));
    }

    [Fact]
    public async Task GetThisYear_liefert_zwölf_Monatsbalken()
    {
        (StatisticsService service, _) = CreateService();

        PeriodStatistics year = await service.GetThisYearAsync();

        Assert.Equal(12, year.Buckets.Count);
        Assert.Equal(3, year.Count);
        Assert.Equal(3, year.Buckets.Sum(bucket => bucket.Count));
    }

    [Fact]
    public void CsvExporter_erzeugt_Kopfzeile_und_eine_Zeile_pro_Sitzung()
    {
        CsvExporter exporter = new();
        BreakSession[] sessions =
        {
            Session(Now, TimeSpan.FromMinutes(5), BreakOutcome.Taken),
            Session(Now.AddHours(1), TimeSpan.Zero, BreakOutcome.Skipped),
        };

        string csv = exporter.Export(sessions);
        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("StartedAt,EndedAt,Duration,Model,Outcome", lines[0]);
        Assert.Equal(3, lines.Length); // Kopf + 2 Zeilen
        Assert.Contains("Taken", lines[1], StringComparison.Ordinal);
        Assert.Contains("Skipped", lines[2], StringComparison.Ordinal);
    }
}
