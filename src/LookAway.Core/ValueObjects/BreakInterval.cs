namespace LookAway.Core.ValueObjects;

/// <summary>
/// Arbeits-/Pausen-Intervall eines Pausenmodells als Rich-Domain-Value-Object.
/// </summary>
/// <remarks>
/// Konstruktion ausschließlich über die Factory <see cref="Create"/>, damit die
/// Invarianten (Wertebereiche und die Querbedingung <c>MaxLimit ≥ WorkDuration</c>)
/// nie verletzt werden können. <see cref="MaxLimit"/> ist nur bei
/// aufgabenbasierten Modellen gesetzt und begrenzt die maximale Arbeitsdauer
/// ohne Pause.
/// </remarks>
public sealed record BreakInterval
{
    /// <summary>
    /// Untergrenze für Arbeitsdauer (5 Minuten). So lang ist auch das Zurückstellen
    /// (<see cref="Domain.BreakDefaults.SnoozeWorkDuration"/>); kürzer wäre die Erinnerung
    /// eine Dauerstörung.
    /// </summary>
    public static readonly TimeSpan MinWorkDuration = TimeSpan.FromMinutes(5);

    /// <summary>Obergrenze für Arbeitsdauer (8 Stunden, ein Arbeitstag).</summary>
    public static readonly TimeSpan MaxWorkDuration = TimeSpan.FromHours(8);

    /// <summary>Untergrenze für Pausendauer (1 Minute).</summary>
    public static readonly TimeSpan MinBreakDuration = TimeSpan.FromMinutes(1);

    /// <summary>Obergrenze für Pausendauer (2 Stunden); länger ist keine Bildschirmpause mehr, sondern Feierabend.</summary>
    public static readonly TimeSpan MaxBreakDuration = TimeSpan.FromHours(2);

    private BreakInterval()
    {
    }

    /// <summary>Dauer einer Arbeitsphase.</summary>
    public TimeSpan WorkDuration { get; private init; }

    /// <summary>Dauer einer Pause.</summary>
    public TimeSpan BreakDuration { get; private init; }

    /// <summary>
    /// Maximale Arbeitsdauer ohne Pause (nur bei aufgabenbasierten Modellen).
    /// <c>null</c> bedeutet kein Limit.
    /// </summary>
    public TimeSpan? MaxLimit { get; private init; }

    /// <summary>
    /// Erzeugt ein validiertes Intervall.
    /// </summary>
    /// <param name="workDuration">Arbeitsdauer (zwischen <see cref="MinWorkDuration"/> und <see cref="MaxWorkDuration"/>).</param>
    /// <param name="breakDuration">Pausendauer (zwischen <see cref="MinBreakDuration"/> und <see cref="MaxBreakDuration"/>).</param>
    /// <param name="maxLimit">Optionales Arbeits-Maximum; muss &#8805; <paramref name="workDuration"/> sein.</param>
    /// <returns>Das validierte <see cref="BreakInterval"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Ein Wert liegt außerhalb seines gültigen Bereichs.</exception>
    /// <exception cref="ArgumentException"><paramref name="maxLimit"/> ist kleiner als <paramref name="workDuration"/>.</exception>
    public static BreakInterval Create(
        TimeSpan workDuration,
        TimeSpan breakDuration,
        TimeSpan? maxLimit = null)
    {
        EnsureInRange(workDuration, MinWorkDuration, MaxWorkDuration, nameof(workDuration), "WorkDuration");
        EnsureInRange(breakDuration, MinBreakDuration, MaxBreakDuration, nameof(breakDuration), "BreakDuration");

        if (maxLimit is { } limit)
        {
            EnsureInRange(limit, MinWorkDuration, MaxWorkDuration, nameof(maxLimit), "MaxLimit");

            if (limit < workDuration)
            {
                throw new ArgumentException(
                    $"MaxLimit ({limit}) darf nicht kleiner als WorkDuration ({workDuration}) sein.",
                    nameof(maxLimit));
            }
        }

        return new BreakInterval
        {
            WorkDuration = workDuration,
            BreakDuration = breakDuration,
            MaxLimit = maxLimit,
        };
    }

    private static void EnsureInRange(TimeSpan value, TimeSpan min, TimeSpan max, string parameterName, string label)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{label} muss zwischen {min} und {max} liegen.");
        }
    }
}
