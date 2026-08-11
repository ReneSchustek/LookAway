using LookAway.Core.Entities;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Persistenz der Aufgaben des aufgabenbasierten Pausenmodells.
/// </summary>
public interface IWorkTaskRepository
{
    /// <summary>
    /// Liest alle Aufgaben.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Die gespeicherten Aufgaben; eine leere Liste, wenn noch keine angelegt wurde.</returns>
    Task<IReadOnlyList<WorkTask>> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt eine Aufgabe an oder ersetzt die vorhandene mit derselben Kennung.
    /// </summary>
    /// <param name="task">Die zu speichernde Aufgabe.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    Task SaveAsync(WorkTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Entfernt eine Aufgabe.
    /// </summary>
    /// <param name="id">Kennung der zu entfernenden Aufgabe.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns><c>true</c>, wenn eine Aufgabe entfernt wurde; <c>false</c>, wenn keine mit dieser Kennung vorlag.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
