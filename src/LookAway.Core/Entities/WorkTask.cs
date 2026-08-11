using System.Text.Json.Serialization;

namespace LookAway.Core.Entities;

/// <summary>
/// Eine Aufgabe, an der beim aufgabenbasierten Pausenmodell gearbeitet wird.
/// </summary>
/// <remarks>
/// Unveränderlich wie <see cref="BreakSession"/>: Abschließen, Wiederöffnen und
/// Umbenennen liefern eine neue Fassung. Damit kann keine Liste halb aktualisiert
/// dastehen, wenn beim Schreiben etwas schiefgeht.
/// </remarks>
public sealed class WorkTask
{
    /// <summary>Obergrenze der Textlänge.</summary>
    /// <remarks>
    /// Eine Kachel mit einem Absatz Text ist keine Aufgabe mehr, sondern eine Notiz —
    /// und sie sprengt die Liste, in der sie steht.
    /// </remarks>
    public const int MaxTextLength = 200;

    /// <summary>
    /// Erzeugt eine validierte Aufgabe. Für die Deserialisierung und für die
    /// Fabrikmethoden gedacht; neue Aufgaben entstehen über <see cref="Create"/>.
    /// </summary>
    /// <param name="id">Eindeutige Kennung.</param>
    /// <param name="text">Beschreibung der Aufgabe.</param>
    /// <param name="createdAt">Zeitpunkt der Anlage.</param>
    /// <param name="completedAt">Zeitpunkt des Abschlusses; <c>null</c>, solange offen.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> ist leer oder der Text unbrauchbar.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Der Abschluss liegt vor der Anlage.</exception>
    [JsonConstructor]
    public WorkTask(Guid id, string text, DateTimeOffset createdAt, DateTimeOffset? completedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id darf nicht leer sein.", nameof(id));
        }

        if (completedAt is { } ende && ende < createdAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAt),
                completedAt,
                "Der Abschluss darf nicht vor der Anlage liegen.");
        }

        Id = id;
        Text = Validate(text, nameof(text));
        CreatedAt = createdAt;
        CompletedAt = completedAt;
    }

    /// <summary>Eindeutige Kennung der Aufgabe.</summary>
    public Guid Id { get; }

    /// <summary>Beschreibung der Aufgabe.</summary>
    public string Text { get; }

    /// <summary>Zeitpunkt der Anlage.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Zeitpunkt des Abschlusses; <c>null</c>, solange die Aufgabe offen ist.</summary>
    public DateTimeOffset? CompletedAt { get; }

    /// <summary>Wahr, wenn die Aufgabe abgeschlossen ist.</summary>
    [JsonIgnore]
    public bool IsCompleted => CompletedAt.HasValue;

    /// <summary>
    /// Legt eine neue, offene Aufgabe an.
    /// </summary>
    /// <param name="text">Beschreibung; führende und folgende Leerzeichen entfallen.</param>
    /// <param name="createdAt">Zeitpunkt der Anlage.</param>
    /// <returns>Die angelegte Aufgabe.</returns>
    /// <exception cref="ArgumentException">Der Text ist leer oder zu lang.</exception>
    public static WorkTask Create(string text, DateTimeOffset createdAt)
        => new(Guid.NewGuid(), text, createdAt, completedAt: null);

    /// <summary>
    /// Liefert die Aufgabe als abgeschlossen.
    /// </summary>
    /// <param name="completedAt">Zeitpunkt des Abschlusses.</param>
    /// <returns>Die abgeschlossene Fassung.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Der Zeitpunkt liegt vor der Anlage.</exception>
    public WorkTask Complete(DateTimeOffset completedAt)
        => new(Id, Text, CreatedAt, completedAt);

    /// <summary>
    /// Liefert die Aufgabe wieder als offen.
    /// </summary>
    /// <returns>Die offene Fassung.</returns>
    public WorkTask Reopen() => new(Id, Text, CreatedAt, completedAt: null);

    /// <summary>
    /// Liefert die Aufgabe mit geändertem Text; der Zustand bleibt.
    /// </summary>
    /// <param name="text">Neue Beschreibung.</param>
    /// <returns>Die umbenannte Fassung.</returns>
    /// <exception cref="ArgumentException">Der Text ist leer oder zu lang.</exception>
    public WorkTask WithText(string text) => new(Id, text, CreatedAt, CompletedAt);

    private static string Validate(string text, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, parameterName);

        string trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength)
        {
            throw new ArgumentException(
                $"Der Text darf höchstens {MaxTextLength} Zeichen lang sein.",
                parameterName);
        }

        return trimmed;
    }
}
