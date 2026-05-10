using System.Diagnostics;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Time;

/// <summary>
/// Standard-<see cref="IClock"/>-Implementation: liest die Wallclock-Uhr
/// und kapselt einen Prozess-globalen <see cref="Stopwatch"/> fuer
/// Diagnostik-Zwecke.
/// </summary>
public sealed class SystemClock : IClock
{
    private static readonly Stopwatch ProcessStopwatch = Stopwatch.StartNew();

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public TimeSpan MonotonicElapsed => ProcessStopwatch.Elapsed;
}
