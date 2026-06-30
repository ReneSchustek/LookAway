namespace LookAway.Core.ValueObjects;

/// <summary>
/// Optionale Benutzer-Überschreibung der Arbeits- und Pausendauer in Minuten.
/// Gilt zusätzlich zu den Defaults des aktiven Pausenmodells.
/// </summary>
public sealed record CustomDurations
{
    /// <summary>Minimale Arbeitsdauer in Minuten (entspricht dem Domänen-Minimum von <see cref="BreakInterval.MinWorkDuration"/>).</summary>
    public const int MinWorkMinutes = 5;

    /// <summary>Maximale Arbeitsdauer in Minuten (8 Stunden).</summary>
    public const int MaxWorkMinutes = 480;

    /// <summary>Minimale Pausendauer in Minuten (entspricht dem Domänen-Minimum von <see cref="BreakInterval.MinBreakDuration"/>).</summary>
    public const int MinBreakMinutes = 1;

    /// <summary>Maximale Pausendauer in Minuten (2 Stunden).</summary>
    public const int MaxBreakMinutes = 120;

    private int _workMinutes;
    private int _breakMinutes;

    /// <summary>
    /// Arbeitsdauer in Minuten. Gültig: <see cref="MinWorkMinutes"/> bis <see cref="MaxWorkMinutes"/>.
    /// </summary>
    public required int WorkMinutes
    {
        get => _workMinutes;
        init
        {
            if (value is < MinWorkMinutes or > MaxWorkMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"WorkMinutes muss zwischen {MinWorkMinutes} und {MaxWorkMinutes} liegen.");
            }
            _workMinutes = value;
        }
    }

    /// <summary>
    /// Pausendauer in Minuten. Gültig: <see cref="MinBreakMinutes"/> bis <see cref="MaxBreakMinutes"/>.
    /// </summary>
    public required int BreakMinutes
    {
        get => _breakMinutes;
        init
        {
            if (value is < MinBreakMinutes or > MaxBreakMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"BreakMinutes muss zwischen {MinBreakMinutes} und {MaxBreakMinutes} liegen.");
            }
            _breakMinutes = value;
        }
    }
}
