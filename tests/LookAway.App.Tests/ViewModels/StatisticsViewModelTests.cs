using LookAway.App.Tests.Fakes;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Services;
using LookAway.App.ViewModels;

namespace LookAway.App.Tests;

/// <summary>
/// Tests für das Statistik-ViewModel: Laden der Kennzahlen und CSV-Export.
/// </summary>
public sealed class StatisticsViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    private static BreakSession Session(DateTimeOffset start, TimeSpan duration, BreakOutcome outcome)
        => new(Guid.NewGuid(), start, start + duration, BreakModel.ClassicPomodoro, outcome);

    [Fact]
    public async Task StatisticsViewModel_Export_löst_Ereignis_mit_CSV_aus()
    {
        FakeBreakHistoryRepository history = new(new[]
        {
            Session(Now, TimeSpan.FromMinutes(5), BreakOutcome.Taken),
        });
        FakeClock clock = new(Now);
        StatisticsViewModel viewModel = new(
            new StatisticsService(history, clock),
            history,
            new CsvExporter(),
            new FakeLocalizationService());
        string? exported = null;
        viewModel.CsvExportRequested += (_, e) => exported = e.Content;

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.NotNull(exported);
        Assert.Contains("StartedAt,EndedAt,Duration,Model,Outcome", exported, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatisticsViewModel_Load_befüllt_Heute_und_Balken()
    {
        FakeBreakHistoryRepository history = new(new[]
        {
            Session(Now, TimeSpan.FromMinutes(5), BreakOutcome.Taken),
        });
        FakeClock clock = new(Now);
        StatisticsViewModel viewModel = new(
            new StatisticsService(history, clock),
            history,
            new CsvExporter(),
            new FakeLocalizationService());

        await viewModel.LoadAsync();

        Assert.Equal(1, viewModel.TodayCount);
        Assert.Equal(7, viewModel.WeekBars.Count);
        Assert.Equal(12, viewModel.YearBars.Count);
    }
}
