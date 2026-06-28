namespace LookAway.Core.Interfaces;

/// <summary>
/// Pausiert und reaktiviert die System-Medienwiedergabe waehrend einer Pause
///. Implementierungen kapseln die Plattform-Anbindung (SMTC).
/// </summary>
public interface IMediaController
{
    /// <summary>
    /// Pausiert alle aktiven Wiedergabe-Sessions und merkt sich, welche liefen.
    /// Fehler (SMTC nicht verfuegbar) werden geschluckt.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    Task PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Setzt nur die Sessions fort, die vor <see cref="PauseAllAsync"/> liefen.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    Task ResumeAllAsync(CancellationToken cancellationToken = default);
}
