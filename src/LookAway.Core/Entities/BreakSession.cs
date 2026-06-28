using System.Text.Json.Serialization;
using LookAway.Core.Enums;

namespace LookAway.Core.Entities;

/// <summary>
/// Aufgezeichnete Pause-Erinnerung mit Zeitraum, Modell und Ergebnis.
/// Unveraenderlich; die Invarianten werden im Konstruktor geprueft.
/// </summary>
public sealed class BreakSession
{
    /// <summary>
    /// Erzeugt eine validierte Sitzung.
    /// </summary>
    /// <param name="id">Eindeutige Kennung.</param>
    /// <param name="startedAt">Beginn (Anzeige der Erinnerung).</param>
    /// <param name="endedAt">Ende; darf nicht vor <paramref name="startedAt"/> liegen.</param>
    /// <param name="model">Aktives Pausenmodell.</param>
    /// <param name="outcome">Ergebnis der Erinnerung.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> ist leer.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Ende liegt vor Beginn oder Enum-Wert ist unbekannt.</exception>
    [JsonConstructor]
    public BreakSession(
        Guid id,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        BreakModel model,
        BreakOutcome outcome)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id darf nicht leer sein.", nameof(id));
        }

        if (endedAt < startedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endedAt),
                endedAt,
                "EndedAt darf nicht vor StartedAt liegen.");
        }

        if (!Enum.IsDefined(model))
        {
            throw new ArgumentOutOfRangeException(nameof(model), model, "Unbekanntes Pausenmodell.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unbekanntes Ergebnis.");
        }

        Id = id;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Model = model;
        Outcome = outcome;
    }

    /// <summary>Eindeutige Kennung der Sitzung.</summary>
    public Guid Id { get; }

    /// <summary>Beginn (Anzeige der Erinnerung).</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Ende der Sitzung.</summary>
    public DateTimeOffset EndedAt { get; }

    /// <summary>Aktives Pausenmodell.</summary>
    public BreakModel Model { get; }

    /// <summary>Ergebnis der Erinnerung.</summary>
    public BreakOutcome Outcome { get; }

    /// <summary>Dauer der Sitzung.</summary>
    [JsonIgnore]
    public TimeSpan Duration => EndedAt - StartedAt;
}
