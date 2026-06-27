namespace LookAway.Core.ValueObjects;

/// <summary>
/// Arbeits-/Pausen-Intervall eines Pausenmodells als Rich-Domain-Value-Object.
/// </summary>
/// <remarks>
/// Konstruktion ausschliesslich ueber die Factory <see cref="Create"/>, damit die
/// Invarianten (Wertebereiche und die Querbedingung <c>MaxLimit ≥ WorkDuration</c>)
/// nie verletzt werden koennen. <see cref="MaxLimit"/> ist nur bei
/// aufgabenbasierten Modellen gesetzt und begrenzt die maximale Arbeitsdauer
/// ohne Pause.
/// </remarks>
public sealed record BreakInterval
{
    /// <summary>Untergrenze fuer Arbeitsdauer (5 Minuten).</summary>
    public static readonly TimeSpan MinWorkDuration = TimeSpan.FromMinutes(5);

    /// <summary>Obergrenze fuer Arbeitsdauer (8 Stunden).</summary>
    public static readonly TimeSpan MaxWorkDuration = TimeSpan.FromHours(8);

    /// <summary>Untergrenze fuer Pausendauer (1 Minute).</summary>
    public static readonly TimeSpan MinBreakDuration = TimeSpan.FromMinutes(1);

    /// <summary>Obergrenze fuer Pausendauer (2 Stunden).</summary>
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
    /// <exception cref="ArgumentOutOfRangeException">Ein Wert liegt ausserhalb seines gueltigen Bereichs.</exception>
    /// <exception cref="ArgumentException"><paramref name="maxLimit"/> ist kleiner als <paramref name="workDuration"/>.</exception>
    public static BreakInterval Create(
        TimeSpan workDuration,
        TimeSpan breakDuration,
        TimeSpan? maxLimit = null)
    {
        if (workDuration < MinWorkDuration || workDuration > MaxWorkDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workDuration),
                workDuration,
                $"WorkDuration muss zwischen {MinWorkDuration} und {MaxWorkDuration} liegen.");
        }

        if (breakDuration < MinBreakDuration || breakDuration > MaxBreakDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(breakDuration),
                breakDuration,
                $"BreakDuration muss zwischen {MinBreakDuration} und {MaxBreakDuration} liegen.");
        }

        if (maxLimit is { } limit)
        {
            if (limit < MinWorkDuration || limit > MaxWorkDuration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxLimit),
                    maxLimit,
                    $"MaxLimit muss zwischen {MinWorkDuration} und {MaxWorkDuration} liegen.");
            }

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
}
