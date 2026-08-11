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
    public async Task StatisticsViewModel_Export_RaisesTheEventWithCsv()
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
    public async Task StatisticsViewModel_Load_FillsTodayAndTheBars()
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

    /// <remarks>
    /// Die Überschriften stehen über den drei Zeiträumen und den Kennzahlen darunter.
    /// Eine leere Überschrift lässt eine Kennzahl ohne Bedeutung dastehen.
    /// </remarks>
    [Fact]
    public async Task EveryLabelReturnsText()
    {
        FakeBreakHistoryRepository history = new(
        [
            Session(Now, TimeSpan.FromMinutes(5), BreakOutcome.Taken),
        ]);
        StatisticsViewModel viewModel = new(
            new StatisticsService(history, new FakeClock(Now)),
            history,
            new CsvExporter(),
            new FakeLocalizationService());
        await viewModel.LoadAsync();
        List<string> empty = [];

        foreach (System.Reflection.PropertyInfo property in typeof(StatisticsViewModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead))
        {
            if (property.GetValue(viewModel) is not string value || string.IsNullOrWhiteSpace(value))
            {
                empty.Add(property.Name);
            }
        }

        Assert.True(empty.Count == 0, "Ohne Text: " + string.Join(", ", empty));
    }
}
