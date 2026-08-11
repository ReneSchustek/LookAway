using LookAway.Core.Entities;
using LookAway.Core.Interfaces;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// In-Memory-Fake für <see cref="IWorkTaskRepository"/>. Hält die Aufgaben in einer
/// Liste und merkt sich, was gespeichert und gelöscht wurde.
/// </summary>
internal sealed class FakeWorkTaskRepository : IWorkTaskRepository
{
    private readonly List<WorkTask> _tasks;

    /// <summary>Erzeugt den Fake, optional mit Startdaten.</summary>
    /// <param name="initial">Anfangs vorhandene Aufgaben.</param>
    public FakeWorkTaskRepository(IEnumerable<WorkTask>? initial = null)
        => _tasks = initial is null ? [] : [.. initial];

    /// <summary>Die gespeicherten Aufgaben in der Reihenfolge der Aufrufe.</summary>
    public List<WorkTask> Saved { get; } = [];

    /// <summary>Die Kennungen der gelöschten Aufgaben.</summary>
    public List<Guid> Deleted { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkTask>> LoadAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkTask>>([.. _tasks]);

    /// <inheritdoc />
    public Task SaveAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        Saved.Add(task);

        int index = _tasks.FindIndex(existing => existing.Id == task.Id);
        if (index >= 0)
        {
            _tasks[index] = task;
        }
        else
        {
            _tasks.Add(task);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        return Task.FromResult(_tasks.RemoveAll(task => task.Id == id) > 0);
    }
}
