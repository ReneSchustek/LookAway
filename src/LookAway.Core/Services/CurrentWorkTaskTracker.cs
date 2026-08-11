using LookAway.Core.Entities;
using LookAway.Core.Interfaces;

namespace LookAway.Core.Services;

/// <summary>
/// Hält fest, an welcher Aufgabe gerade gearbeitet wird.
/// </summary>
/// <remarks>
/// Der Timer-Ablauf braucht die Antwort <em>sofort</em>: Wenn die Pause aufgezeichnet
/// wird, ist kein Platz für einen Dateizugriff. Deshalb steht die Antwort hier im
/// Speicher und wird nachgeführt, sobald sich an den Aufgaben etwas ändert.
///
/// Als laufende Aufgabe gilt die <b>zuletzt angelegte offene</b>. Das braucht keine
/// zusätzliche Auswahl durch den Benutzer: Wer eine Aufgabe anlegt, fängt damit an —
/// und wer sie abhakt, arbeitet an der davor weiter.
/// </remarks>
public sealed class CurrentWorkTaskTracker
{
    private readonly IWorkTaskRepository _repository;

    /// <summary>Erzeugt den Träger.</summary>
    /// <param name="repository">Quelle der Aufgaben.</param>
    public CurrentWorkTaskTracker(IWorkTaskRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>Kennung der laufenden Aufgabe; <c>null</c>, wenn keine offen ist.</summary>
    public Guid? CurrentTaskId { get; private set; }

    /// <summary>Text der laufenden Aufgabe; <c>null</c>, wenn keine offen ist.</summary>
    public string? CurrentTaskText { get; private set; }

    /// <summary>
    /// Liest die Aufgaben und übernimmt die zuletzt angelegte offene als laufende.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkTask> tasks = await _repository.LoadAllAsync(cancellationToken).ConfigureAwait(false);

        WorkTask? current = tasks
            .Where(task => !task.IsCompleted)
            .OrderByDescending(task => task.CreatedAt)
            .FirstOrDefault();

        CurrentTaskId = current?.Id;
        CurrentTaskText = current?.Text;
    }
}
