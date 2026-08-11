using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für <see cref="CurrentWorkTaskTracker"/>: Welche Aufgabe als die laufende gilt.
/// </summary>
public sealed class CurrentWorkTaskTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefreshAsync_TakesTheNewestOpenTask()
    {
        WorkTask older = WorkTask.Create("Ältere Aufgabe", Now.AddDays(-1));
        WorkTask newer = WorkTask.Create("Neuere Aufgabe", Now);
        CurrentWorkTaskTracker tracker = new(new StubRepository([older, newer]));

        await tracker.RefreshAsync();

        Assert.Equal(newer.Id, tracker.CurrentTaskId);
        Assert.Equal("Neuere Aufgabe", tracker.CurrentTaskText);
    }

    /// <remarks>
    /// Wer die neueste Aufgabe abhakt, arbeitet an der davor weiter — genau das muss
    /// der Träger nach dem Nachführen abbilden.
    /// </remarks>
    [Fact]
    public async Task RefreshAsync_SkipsCompletedTasks()
    {
        WorkTask openTask = WorkTask.Create("Bleibt openTask", Now.AddDays(-1));
        WorkTask doneTask = WorkTask.Create("Schon fertig", Now).Complete(Now.AddHours(1));
        CurrentWorkTaskTracker tracker = new(new StubRepository([openTask, doneTask]));

        await tracker.RefreshAsync();

        Assert.Equal(openTask.Id, tracker.CurrentTaskId);
    }

    [Fact]
    public async Task RefreshAsync_ReportsNothingWithoutOpenTasks()
    {
        CurrentWorkTaskTracker tracker = new(new StubRepository(
            [WorkTask.Create("Alles doneTask", Now).Complete(Now.AddHours(1))]));

        await tracker.RefreshAsync();

        Assert.Null(tracker.CurrentTaskId);
        Assert.Null(tracker.CurrentTaskText);
    }

    [Fact]
    public async Task RefreshAsync_ReportsNothingWithoutAnyTask()
    {
        CurrentWorkTaskTracker tracker = new(new StubRepository([]));

        await tracker.RefreshAsync();

        Assert.Null(tracker.CurrentTaskId);
    }

    /// <remarks>
    /// Nachführen heißt auch: zurücknehmen. Wird die letzte offene Aufgabe abgehakt,
    /// darf im Infobereich nicht weiter ihr Name stehen.
    /// </remarks>
    [Fact]
    public async Task RefreshAsync_ClearsThePreviousTask()
    {
        StubRepository repository = new([WorkTask.Create("Erst openTask", Now)]);
        CurrentWorkTaskTracker tracker = new(repository);
        await tracker.RefreshAsync();
        Assert.True(tracker.CurrentTaskId.HasValue);

        repository.Tasks = Array.Empty<WorkTask>();
        await tracker.RefreshAsync();

        Assert.Null(tracker.CurrentTaskId);
        Assert.Null(tracker.CurrentTaskText);
    }

    [Fact]
    public void Constructor_RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => new CurrentWorkTaskTracker(null!));

    /// <summary>Schlanker Ersatz für das Repository.</summary>
    private sealed class StubRepository(IReadOnlyList<WorkTask> tasks) : IWorkTaskRepository
    {
        public IReadOnlyList<WorkTask> Tasks { get; set; } = tasks;

        public Task<IReadOnlyList<WorkTask>> LoadAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Tasks);

        public Task SaveAsync(WorkTask task, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
