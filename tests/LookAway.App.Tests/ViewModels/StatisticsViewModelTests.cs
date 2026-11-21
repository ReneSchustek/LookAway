using LookAway.App.Tests.Fakes;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Localization;
using LookAway.Core.Services;
using LookAway.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Das Statistik-ViewModel füllt die Werte für heute und die Balken aus der Historie und
/// reicht den CSV-Export an die Ansicht weiter. Eine leere Statistik fiele sonst erst auf,
/// wenn jemand nachsieht.
/// </summary>
public sealed class StatisticsViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    private static BreakSession Session(DateTimeOffset start, TimeSpan duration, BreakOutcome outcome)
        => new(TestIds.Next(), start, start + duration, BreakModel.ClassicPomodoro, outcome);

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
            new FakeLocalizationService(),
            NullLogger<StatisticsViewModel>.Instance);
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
            new FakeLocalizationService(),
            NullLogger<StatisticsViewModel>.Instance);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

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
            new FakeLocalizationService(),
            NullLogger<StatisticsViewModel>.Instance);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        List<string> empty = [];

        foreach (System.Reflection.PropertyInfo property in typeof(StatisticsViewModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead)
            // Die Export-Rückmeldung ist Zustand, keine Beschriftung: leer, bis ein Export lief.
            .Where(property => property.Name != nameof(StatisticsViewModel.ExportStatusText)))
        {
            if (property.GetValue(viewModel) is not string value || string.IsNullOrWhiteSpace(value))
            {
                empty.Add(property.Name);
            }
        }

        Assert.True(empty.Count == 0, "Ohne Text: " + string.Join(", ", empty));
    }

    /// <remarks>
    /// Ein gescheiterter Export endet sonst ohne jedes Zeichen, und der Benutzer hält die Datei
    /// für geschrieben. Die Rückmeldung muss sagen, was passiert ist, und ein neuer Export setzt
    /// sie zurück, damit keine alte Meldung stehen bleibt.
    /// </remarks>
    [Fact]
    public async Task ExportFeedback_ReportsSavedAndFailed_AndResetsOnTheNextExport()
    {
        FakeBreakHistoryRepository history = new([Session(Now, TimeSpan.FromMinutes(5), BreakOutcome.Taken)]);
        StatisticsViewModel viewModel = new(
            new StatisticsService(history, new FakeClock(Now)),
            history,
            new CsvExporter(),
            new FakeLocalizationService(),
            NullLogger<StatisticsViewModel>.Instance);

        viewModel.ReportExportFailed(new IOException("voll"));
        Assert.EndsWith(SettingsTextKeys.StatisticsExportFailed, viewModel.ExportStatusText, StringComparison.Ordinal);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, viewModel.ExportStatusText);

        viewModel.ReportExportSaved();
        Assert.EndsWith(SettingsTextKeys.StatisticsExportSaved, viewModel.ExportStatusText, StringComparison.Ordinal);
    }
}
