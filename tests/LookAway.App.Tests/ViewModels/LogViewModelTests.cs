using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Tests.ViewModels;

/// <summary>
/// Tests für <see cref="LogViewModel"/>: Suche im Meldungstext, Filter nach Stufe
/// und Zeitraum, und die zwei verschiedenen Leerzustände.
/// </summary>
public sealed class LogViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_ShowsEveryEntry()
    {
        LogViewModel viewModel = CreateViewModel(Entries());

        await viewModel.LoadAsync();

        Assert.Equal(4, viewModel.VisibleEntries.Count);
        Assert.False(viewModel.ShowEmpty);
        Assert.False(viewModel.ShowNoResults);
    }

    [Fact]
    public async Task SearchText_FiltersByMessageWhileTyping()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();

        viewModel.SearchText = "hotkey";

        LogListItem item = Assert.Single(viewModel.VisibleEntries);
        Assert.Contains("Hotkey", item.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LevelFilter_Error_KeepsErrorsAndCriticals()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();

        viewModel.LevelFilter = LogLevelFilter.Error;

        Assert.Equal(2, viewModel.VisibleEntries.Count);
        Assert.All(
            viewModel.VisibleEntries,
            item => Assert.True(item.Level >= LogLevel.Error));
    }

    [Fact]
    public async Task LevelFilter_Warning_KeepsOnlyWarnings()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();

        viewModel.LevelFilter = LogLevelFilter.Warning;

        LogListItem item = Assert.Single(viewModel.VisibleEntries);
        Assert.Equal(LogLevel.Warning, item.Level);
    }

    [Fact]
    public async Task PeriodFilter_Today_DropsOlderEntries()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();

        viewModel.PeriodFilter = LogPeriodFilter.Today;

        Assert.Equal(2, viewModel.VisibleEntries.Count);
        Assert.All(
            viewModel.VisibleEntries,
            item => Assert.Equal(Now.Date, item.Timestamp.LocalDateTime.Date));
    }

    [Fact]
    public async Task PeriodFilter_Week_KeepsTheLastSevenDays()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();

        viewModel.PeriodFilter = LogPeriodFilter.Week;

        // Der 30 Tage alte Eintrag fällt heraus, die drei jüngeren bleiben.
        Assert.Equal(3, viewModel.VisibleEntries.Count);
    }

    [Fact]
    public async Task SearchAndFilterCombine()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();

        viewModel.LevelFilter = LogLevelFilter.Error;
        viewModel.SearchText = "speichern";

        LogListItem item = Assert.Single(viewModel.VisibleEntries);
        Assert.Equal(LogLevel.Error, item.Level);
    }

    /// <remarks>
    /// Die zwei Lagen, die der Leerzustand auseinanderhalten muss: Hier ist wirklich
    /// nichts da — davon kann keine Suche zurücksetzen helfen.
    /// </remarks>
    [Fact]
    public async Task ShowEmpty_IsSetWhenNothingWasLogged()
    {
        LogViewModel viewModel = CreateViewModel([]);

        await viewModel.LoadAsync();

        Assert.True(viewModel.ShowEmpty);
        Assert.False(viewModel.ShowNoResults);
    }

    /// <remarks>
    /// Und hier ist etwas da, nur nicht das Gesuchte — dieser Fall bekommt die
    /// Schaltfläche zum Zurücksetzen.
    /// </remarks>
    [Fact]
    public async Task ShowNoResults_IsSetWhenTheFilterHidesEverything()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();

        viewModel.SearchText = "gibt es nicht";

        Assert.False(viewModel.ShowEmpty);
        Assert.True(viewModel.ShowNoResults);
        Assert.Empty(viewModel.VisibleEntries);
    }

    [Fact]
    public async Task ResetSearch_ClearsTextAndBothFilters()
    {
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();
        viewModel.SearchText = "gibt es nicht";
        viewModel.LevelFilter = LogLevelFilter.Error;
        viewModel.PeriodFilter = LogPeriodFilter.Today;

        viewModel.ResetSearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(LogLevelFilter.All, viewModel.LevelFilter);
        Assert.Equal(LogPeriodFilter.All, viewModel.PeriodFilter);
        Assert.Equal(4, viewModel.VisibleEntries.Count);
    }

    [Fact]
    public async Task LoadAsync_KeepsSearchAndFilter()
    {
        // Neu laden darf die Eingabe nicht wegwerfen — sonst tippt man nach jedem
        // Aktualisieren neu.
        LogViewModel viewModel = CreateViewModel(Entries());
        await viewModel.LoadAsync();
        viewModel.LevelFilter = LogLevelFilter.Warning;

        await viewModel.LoadAsync();

        Assert.Equal(LogLevelFilter.Warning, viewModel.LevelFilter);
        _ = Assert.Single(viewModel.VisibleEntries);
    }

    private static IReadOnlyList<LogEntry> Entries() =>
    [
        new(Now.AddHours(-1), LogLevel.Information, "LookAway.App", "Anwendung gestartet"),
        new(Now.AddHours(-2), LogLevel.Error, "LookAway.Data", "Fehler beim Speichern der Einstellungen"),
        new(Now.AddDays(-3), LogLevel.Warning, "LookAway.Data", "Hotkey konnte nicht registriert werden"),
        new(Now.AddDays(-30), LogLevel.Critical, "LookAway.App", "Unbehandelte Ausnahme"),
    ];

    private static LogViewModel CreateViewModel(IReadOnlyList<LogEntry> entries) => new(
        new FakeLogEntryReader(entries),
        new FakeLocalizationService(),
        new FakeClock(Now));
}
