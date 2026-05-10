namespace LookAway.Core.ValueObjects;

/// <summary>
/// Definiert ein Arbeits-/Pausen-Intervall fuer ein Pausenmodell.
/// </summary>
/// <remarks>
/// Validierung erfolgt in den init-Settern. <see cref="MaxLimit"/> ist nur
/// bei aufgabenbasierten Modellen gesetzt und begrenzt die maximale
/// Arbeitsdauer ohne Pause.
/// </remarks>
public sealed record BreakInterval
{
    /// <summary>Untergrenze fuer Arbeitsdauer (1 Minute).</summary>
    public static readonly TimeSpan MinWorkDuration = TimeSpan.FromMinutes(1);

    /// <summary>Obergrenze fuer Arbeitsdauer (8 Stunden).</summary>
    public static readonly TimeSpan MaxWorkDuration = TimeSpan.FromHours(8);

    /// <summary>Untergrenze fuer Pausendauer (1 Minute).</summary>
    public static readonly TimeSpan MinBreakDuration = TimeSpan.FromMinutes(1);

    /// <summary>Obergrenze fuer Pausendauer (2 Stunden).</summary>
    public static readonly TimeSpan MaxBreakDuration = TimeSpan.FromHours(2);

    private TimeSpan _workDuration;
    private TimeSpan _breakDuration;
    private TimeSpan? _maxLimit;

    /// <summary>Dauer einer Arbeitsphase.</summary>
    public required TimeSpan WorkDuration
    {
        get => _workDuration;
        init
        {
            if (value < MinWorkDuration || value > MaxWorkDuration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"WorkDuration muss zwischen {MinWorkDuration} und {MaxWorkDuration} liegen.");
            }
            _workDuration = value;
        }
    }

    /// <summary>Dauer einer Pause.</summary>
    public required TimeSpan BreakDuration
    {
        get => _breakDuration;
        init
        {
            if (value < MinBreakDuration || value > MaxBreakDuration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"BreakDuration muss zwischen {MinBreakDuration} und {MaxBreakDuration} liegen.");
            }
            _breakDuration = value;
        }
    }

    /// <summary>
    /// Maximale Arbeitsdauer ohne Pause (nur bei aufgabenbasierten Modellen).
    /// <c>null</c> bedeutet kein Limit.
    /// </summary>
    public TimeSpan? MaxLimit
    {
        get => _maxLimit;
        init
        {
            if (value is { } limit && (limit < MinWorkDuration || limit > MaxWorkDuration))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"MaxLimit muss zwischen {MinWorkDuration} und {MaxWorkDuration} liegen.");
            }
            _maxLimit = value;
        }
    }
}
