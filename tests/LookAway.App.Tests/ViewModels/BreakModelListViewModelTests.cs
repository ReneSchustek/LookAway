using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Localization;

namespace LookAway.App.Tests.ViewModels;

/// <summary>
/// Tests für <see cref="BreakModelListViewModel"/>: Suche, Filter und die
/// Unterscheidung der Leerzustände.
/// </summary>
public sealed class BreakModelListViewModelTests
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal)
    {
        [SettingsTextKeys.ForModel(BreakModel.ShortBreaks)] = "Kurze Pausen",
        [SettingsTextKeys.ForModel(BreakModel.ClassicPomodoro)] = "Klassisches Pomodoro",
        [SettingsTextKeys.ForModel(BreakModel.ModifiedPomodoro)] = "Modifiziertes Pomodoro",
        [SettingsTextKeys.ForModel(BreakModel.Ultradian)] = "Ultradianer Rhythmus",
        [SettingsTextKeys.ForModel(BreakModel.PhysicalCounter)] = "Haltungsausgleich",
        [SettingsTextKeys.ForModel(BreakModel.TaskBased)] = "Aufgabenbasiert",
        [SettingsTextKeys.ForModel(BreakModel.LegalCompliance)] = "Gesetzliche Empfehlung",
        [SettingsTextKeys.ModelBreakCount] = "{0} Pausen daraus",
    };

    [Fact]
    public async Task LoadAsync_ShowsEveryModel()
    {
        BreakModelListViewModel viewModel = CreateViewModel();

        await viewModel.LoadAsync();

        Assert.Equal(Enum.GetValues<BreakModel>().Length, viewModel.VisibleModels.Count);
        Assert.True(viewModel.HasResults);
        Assert.False(viewModel.ShowNoResults);
    }

    [Fact]
    public async Task SearchText_FiltersByNameWhileTyping()
    {
        BreakModelListViewModel viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        viewModel.SearchText = "pomodoro";

        Assert.Equal(2, viewModel.VisibleModels.Count);
        Assert.All(
            viewModel.VisibleModels,
            item => Assert.Contains("Pomodoro", item.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchText_IgnoresCaseAndSurroundingSpace()
    {
        BreakModelListViewModel viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        viewModel.SearchText = "  ULTRADIANER  ";

        BreakModelListItem item = Assert.Single(viewModel.VisibleModels);
        Assert.Equal(BreakModel.Ultradian, item.Model);
    }

    [Fact]
    public async Task Filter_Active_KeepsOnlyTheChosenModel()
    {
        BreakModelListViewModel viewModel = CreateViewModel(active: BreakModel.Ultradian);
        await viewModel.LoadAsync();

        viewModel.Filter = BreakModelFilter.Active;

        BreakModelListItem item = Assert.Single(viewModel.VisibleModels);
        Assert.Equal(BreakModel.Ultradian, item.Model);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task Filter_Inactive_DropsTheChosenModel()
    {
        BreakModelListViewModel viewModel = CreateViewModel(active: BreakModel.Ultradian);
        await viewModel.LoadAsync();

        viewModel.Filter = BreakModelFilter.Inactive;

        Assert.Equal(Enum.GetValues<BreakModel>().Length - 1, viewModel.VisibleModels.Count);
        Assert.DoesNotContain(viewModel.VisibleModels, item => item.Model == BreakModel.Ultradian);
    }

    /// <remarks>
    /// Modelle sind fest eingebaut, es gibt also immer welche. Leer wird die Liste
    /// nur durch Suche oder Filter — und genau das muss der Leerzustand sagen,
    /// sonst sucht der Benutzer den Fehler in den Daten.
    /// </remarks>
    [Fact]
    public async Task ShowNoResults_IsSetWhenSearchMatchesNothing()
    {
        BreakModelListViewModel viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        viewModel.SearchText = "gibt es nicht";

        Assert.Empty(viewModel.VisibleModels);
        Assert.False(viewModel.HasResults);
        Assert.True(viewModel.ShowNoResults);
    }

    [Fact]
    public async Task ResetSearch_ClearsTextAndFilter()
    {
        BreakModelListViewModel viewModel = CreateViewModel(active: BreakModel.Ultradian);
        await viewModel.LoadAsync();
        viewModel.SearchText = "gibt es nicht";
        viewModel.Filter = BreakModelFilter.Active;

        viewModel.ResetSearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(BreakModelFilter.All, viewModel.Filter);
        Assert.Equal(Enum.GetValues<BreakModel>().Length, viewModel.VisibleModels.Count);
    }

    [Fact]
    public async Task SetActiveModel_MovesTheMarkToTheNewModel()
    {
        BreakModelListViewModel viewModel = CreateViewModel(active: BreakModel.Ultradian);
        await viewModel.LoadAsync();

        viewModel.SetActiveModel(BreakModel.ClassicPomodoro);

        Assert.True(viewModel.VisibleModels.Single(item => item.Model == BreakModel.ClassicPomodoro).IsActive);
        Assert.False(viewModel.VisibleModels.Single(item => item.Model == BreakModel.Ultradian).IsActive);
    }

    [Fact]
    public async Task SelectCommand_ReportsTheChosenModel()
    {
        BreakModelListViewModel viewModel = CreateViewModel();
        await viewModel.LoadAsync();
        BreakModel? reported = null;
        viewModel.ModelSelected += (_, model) => reported = model;

        viewModel.SelectCommand.Execute(viewModel.VisibleModels.Single(item => item.Model == BreakModel.TaskBased));

        Assert.Equal(BreakModel.TaskBased, reported);
    }

    /// <remarks>
    /// Der Zusammenhang, den die Kachel zeigt: Wie viele Pausen aus diesem Modell
    /// entstanden sind. Ohne ihn wäre die Liste nur eine Auswahl mit mehr Platzbedarf.
    /// </remarks>
    [Fact]
    public async Task LoadAsync_CountsRecordedBreaksPerModel()
    {
        FakeBreakHistoryRepository history = new(
        [
            Session(BreakModel.Ultradian),
            Session(BreakModel.Ultradian),
            Session(BreakModel.ClassicPomodoro),
        ]);
        BreakModelListViewModel viewModel = CreateViewModel(history: history);

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.VisibleModels.Single(item => item.Model == BreakModel.Ultradian).BreakCount);
        Assert.Equal(1, viewModel.VisibleModels.Single(item => item.Model == BreakModel.ClassicPomodoro).BreakCount);
        Assert.Equal(0, viewModel.VisibleModels.Single(item => item.Model == BreakModel.TaskBased).BreakCount);
    }

    private static BreakSession Session(BreakModel model) => new(
        Guid.NewGuid(),
        new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 1, 9, 10, 0, TimeSpan.Zero),
        model,
        BreakOutcome.Taken);

    private static BreakModelListViewModel CreateViewModel(
        BreakModel active = BreakModel.ClassicPomodoro,
        FakeBreakHistoryRepository? history = null)
    {
        BreakModelListViewModel viewModel = new(
            new FakeLocalizationService(table: Texts),
            history ?? new FakeBreakHistoryRepository());
        viewModel.SetActiveModel(active);
        return viewModel;
    }
}
