using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;

namespace LookAway.App.Tests.ViewModels;

/// <summary>
/// Tests für <see cref="WorkTaskListViewModel"/>: Suche, Filter, die beiden
/// Leerzustände und die Wege zum Anlegen, Ändern und Löschen.
/// </summary>
public sealed class WorkTaskListViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_ShowsTasksNewestFirst()
    {
        WorkTask older = WorkTask.Create("Alte Aufgabe", Now.AddDays(-2));
        WorkTask newer = WorkTask.Create("Neue Aufgabe", Now);
        WorkTaskListViewModel viewModel = CreateViewModel([older, newer]);

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.VisibleTasks.Count);
        Assert.Equal("Neue Aufgabe", viewModel.VisibleTasks[0].Text);
    }

    [Fact]
    public async Task SearchText_FiltersByTextWhileTyping()
    {
        WorkTaskListViewModel viewModel = CreateViewModel(
        [
            WorkTask.Create("Angebot für Meier", Now),
            WorkTask.Create("Rechnung prüfen", Now),
        ]);
        await viewModel.LoadAsync();

        viewModel.SearchText = "angebot";

        Assert.Equal("Angebot für Meier", Assert.Single(viewModel.VisibleTasks).Text);
    }

    [Fact]
    public async Task Filter_Open_HidesCompletedTasks()
    {
        WorkTaskListViewModel viewModel = CreateViewModel(
        [
            WorkTask.Create("Offen", Now),
            WorkTask.Create("Fertig", Now).Complete(Now.AddHours(1)),
        ]);
        await viewModel.LoadAsync();

        viewModel.Filter = WorkTaskFilter.Open;

        Assert.Equal("Offen", Assert.Single(viewModel.VisibleTasks).Text);
    }

    [Fact]
    public async Task Filter_Completed_ShowsOnlyFinishedTasks()
    {
        WorkTaskListViewModel viewModel = CreateViewModel(
        [
            WorkTask.Create("Offen", Now),
            WorkTask.Create("Fertig", Now).Complete(Now.AddHours(1)),
        ]);
        await viewModel.LoadAsync();

        viewModel.Filter = WorkTaskFilter.Completed;

        Assert.Equal("Fertig", Assert.Single(viewModel.VisibleTasks).Text);
    }

    /// <remarks>
    /// Noch keine Aufgabe angelegt — davon hilft kein Zurücksetzen der Suche.
    /// </remarks>
    [Fact]
    public async Task ShowEmpty_IsSetWhenNoTaskExists()
    {
        WorkTaskListViewModel viewModel = CreateViewModel([]);

        await viewModel.LoadAsync();

        Assert.True(viewModel.ShowEmpty);
        Assert.False(viewModel.ShowNoResults);
    }

    /// <remarks>
    /// Aufgaben da, nur nicht die gesuchten — dieser Fall bekommt die Schaltfläche.
    /// </remarks>
    [Fact]
    public async Task ShowNoResults_IsSetWhenTheSearchMatchesNothing()
    {
        WorkTaskListViewModel viewModel = CreateViewModel([WorkTask.Create("Angebot", Now)]);
        await viewModel.LoadAsync();

        viewModel.SearchText = "gibt es nicht";

        Assert.False(viewModel.ShowEmpty);
        Assert.True(viewModel.ShowNoResults);
    }

    [Fact]
    public async Task AddCommand_CreatesATaskAndClearsTheInput()
    {
        FakeWorkTaskRepository repository = new();
        WorkTaskListViewModel viewModel = CreateViewModel([], repository);
        await viewModel.LoadAsync();
        viewModel.NewTaskText = "  Angebot schreiben  ";

        await viewModel.AddCommand.ExecuteAsync(parameter: null);

        Assert.Equal("Angebot schreiben", Assert.Single(viewModel.VisibleTasks).Text);
        Assert.Equal(string.Empty, viewModel.NewTaskText);
        _ = Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task AddCommand_IsBlockedWithoutText()
    {
        WorkTaskListViewModel viewModel = CreateViewModel([]);
        await viewModel.LoadAsync();

        viewModel.NewTaskText = "   ";

        Assert.False(viewModel.AddCommand.CanExecute(null));
    }

    [Fact]
    public async Task ToggleCompletedCommand_MarksAndUnmarks()
    {
        FakeWorkTaskRepository repository = new([WorkTask.Create("Ablage", Now)]);
        WorkTaskListViewModel viewModel = CreateViewModel(null, repository);
        await viewModel.LoadAsync();
        WorkTaskListItem item = viewModel.VisibleTasks[0];

        await viewModel.ToggleCompletedCommand.ExecuteAsync(item);
        Assert.True(viewModel.VisibleTasks[0].IsCompleted);

        await viewModel.ToggleCompletedCommand.ExecuteAsync(viewModel.VisibleTasks[0]);
        Assert.False(viewModel.VisibleTasks[0].IsCompleted);
    }

    [Fact]
    public async Task DeleteCommand_RemovesTheTask()
    {
        FakeWorkTaskRepository repository = new([WorkTask.Create("Weg damit", Now)]);
        WorkTaskListViewModel viewModel = CreateViewModel(null, repository);
        await viewModel.LoadAsync();

        await viewModel.DeleteCommand.ExecuteAsync(viewModel.VisibleTasks[0]);

        Assert.Empty(viewModel.VisibleTasks);
        Assert.True(viewModel.ShowEmpty);
    }

    [Fact]
    public async Task CommitEditCommand_RenamesTheTask()
    {
        FakeWorkTaskRepository repository = new([WorkTask.Create("Alter Text", Now)]);
        WorkTaskListViewModel viewModel = CreateViewModel(null, repository);
        await viewModel.LoadAsync();
        WorkTaskListItem item = viewModel.VisibleTasks[0];

        viewModel.StartEditCommand.Execute(item);
        item.EditText = "Neuer Text";
        await viewModel.CommitEditCommand.ExecuteAsync(item);

        Assert.Equal("Neuer Text", viewModel.VisibleTasks[0].Text);
        Assert.False(viewModel.VisibleTasks[0].IsEditing);
    }

    /// <remarks>
    /// Ein leerer Text darf die Aufgabe nicht namenlos machen — die Änderung wird
    /// verworfen und die Aufgabe bleibt, wie sie war.
    /// </remarks>
    [Fact]
    public async Task CommitEditCommand_KeepsTheOldTextWhenTheInputIsEmpty()
    {
        FakeWorkTaskRepository repository = new([WorkTask.Create("Bleibt so", Now)]);
        WorkTaskListViewModel viewModel = CreateViewModel(null, repository);
        await viewModel.LoadAsync();
        WorkTaskListItem item = viewModel.VisibleTasks[0];

        viewModel.StartEditCommand.Execute(item);
        item.EditText = "   ";
        await viewModel.CommitEditCommand.ExecuteAsync(item);

        Assert.Equal("Bleibt so", viewModel.VisibleTasks[0].Text);
    }

    [Fact]
    public async Task CancelEditCommand_DiscardsTheChange()
    {
        WorkTaskListViewModel viewModel = CreateViewModel([WorkTask.Create("Unverändert", Now)]);
        await viewModel.LoadAsync();
        WorkTaskListItem item = viewModel.VisibleTasks[0];

        viewModel.StartEditCommand.Execute(item);
        item.EditText = "Verworfen";
        viewModel.CancelEditCommand.Execute(item);

        Assert.Equal("Unverändert", viewModel.VisibleTasks[0].Text);
        Assert.False(viewModel.VisibleTasks[0].IsEditing);
    }

    [Fact]
    public async Task ResetSearchCommand_ClearsTextAndFilter()
    {
        WorkTaskListViewModel viewModel = CreateViewModel([WorkTask.Create("Angebot", Now)]);
        await viewModel.LoadAsync();
        viewModel.SearchText = "nichts";
        viewModel.Filter = WorkTaskFilter.Completed;

        viewModel.ResetSearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(WorkTaskFilter.All, viewModel.Filter);
        _ = Assert.Single(viewModel.VisibleTasks);
    }

    /// <remarks>
    /// Der Zusammenhang, um den es geht: Wie viele Pausen an der Aufgabe hingen.
    /// Ohne ihn wäre die Liste eine Aufgabenliste neben der, in der die Aufgaben
    /// ohnehin stehen.
    /// </remarks>
    [Fact]
    public async Task LoadAsync_CountsBreaksPerTask()
    {
        WorkTask task = WorkTask.Create("Mit Pausen", Now);
        WorkTask ohne = WorkTask.Create("Ohne Pausen", Now);
        FakeBreakHistoryRepository history = new(
        [
            Session(task.Id),
            Session(task.Id),
            Session(taskId: null),
        ]);
        WorkTaskListViewModel viewModel = CreateViewModel([task, ohne], history: history);

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.VisibleTasks.Single(item => item.Id == task.Id).BreakCount);
        Assert.Equal(0, viewModel.VisibleTasks.Single(item => item.Id == ohne.Id).BreakCount);
    }

    /// <remarks>
    /// Je Leiste bleibt genau ein Chip gewählt. Der erneute Klick auf den aktiven meldet
    /// „abgewählt" — bliebe das stehen, zeigte die Liste einen Ausschnitt, ohne dass zu
    /// sehen wäre, welchen.
    /// </remarks>
    [Theory]
    [InlineData(nameof(WorkTaskListViewModel.IsFilterAll))]
    [InlineData(nameof(WorkTaskListViewModel.IsFilterOpen))]
    [InlineData(nameof(WorkTaskListViewModel.IsFilterCompleted))]
    public async Task FilterChip_StaysSelectedWhenClickedAgain(string chip)
    {
        WorkTaskListViewModel viewModel = CreateViewModel([WorkTask.Create("Ablage", Now)]);
        await viewModel.LoadAsync();
        SetChip(viewModel, chip, value: true);

        SetChip(viewModel, chip, value: false);

        Assert.True(ChipValue(viewModel, chip));
    }

    [Theory]
    [InlineData(nameof(WorkTaskListViewModel.IsFilterOpen))]
    [InlineData(nameof(WorkTaskListViewModel.IsFilterCompleted))]
    public async Task FilterChip_TakesTheSelectionFromAll(string chosen)
    {
        WorkTaskListViewModel viewModel = CreateViewModel([WorkTask.Create("Ablage", Now)]);
        await viewModel.LoadAsync();

        SetChip(viewModel, chosen, value: true);

        Assert.True(ChipValue(viewModel, chosen));
        Assert.False(viewModel.IsFilterAll);
    }

    /// <remarks>
    /// Die Befehle hängen an den Schaltflächen der Listeneinträge. Kommt von dort nichts
    /// an — etwa weil der Eintrag beim Klicken gerade entfernt wurde —, darf das nicht
    /// zum Absturz führen.
    /// </remarks>
    [Fact]
    public async Task ItemCommands_IgnoreAMissingItem()
    {
        WorkTaskListViewModel viewModel = CreateViewModel([WorkTask.Create("Ablage", Now)]);
        await viewModel.LoadAsync();

        viewModel.StartEditCommand.Execute(null);
        viewModel.CancelEditCommand.Execute(null);
        await viewModel.CommitEditCommand.ExecuteAsync(null);

        _ = Assert.Single(viewModel.VisibleTasks);
    }

    [Fact]
    public void EveryLabelReturnsText()
    {
        WorkTaskListViewModel viewModel = CreateViewModel([]);
        List<string> empty = [];

        foreach (System.Reflection.PropertyInfo property in typeof(WorkTaskListViewModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead))
        {
            if (property.GetValue(viewModel) is not string value || string.IsNullOrWhiteSpace(value))
            {
                empty.Add(property.Name);
            }
        }

        // Die beiden Eingabefelder starten leer; alles Übrige ist Beschriftung.
        _ = empty.Remove(nameof(WorkTaskListViewModel.SearchText));
        _ = empty.Remove(nameof(WorkTaskListViewModel.NewTaskText));

        Assert.True(empty.Count == 0, "Ohne Text: " + string.Join(", ", empty));
    }

    private static void SetChip(WorkTaskListViewModel viewModel, string chip, bool value)
        => typeof(WorkTaskListViewModel).GetProperty(chip)!.SetValue(viewModel, value);

    private static bool ChipValue(WorkTaskListViewModel viewModel, string chip)
        => (bool)typeof(WorkTaskListViewModel).GetProperty(chip)!.GetValue(viewModel)!;

    private static BreakSession Session(Guid? taskId) => new(
        Guid.NewGuid(),
        Now,
        Now.AddMinutes(10),
        BreakModel.TaskBased,
        BreakOutcome.Taken,
        taskId);

    private static WorkTaskListViewModel CreateViewModel(
        IReadOnlyList<WorkTask>? tasks,
        FakeWorkTaskRepository? repository = null,
        FakeBreakHistoryRepository? history = null)
        => new(
            repository ?? new FakeWorkTaskRepository(tasks),
            history ?? new FakeBreakHistoryRepository(),
            new FakeLocalizationService(),
            new FakeClock(Now));
}
