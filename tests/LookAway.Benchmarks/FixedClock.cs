using LookAway.Core.Interfaces;

namespace LookAway.Benchmarks;

/// <summary>
/// Uhr mit festem Zeitpunkt: Die Statistik rechnet „heute“, „diese Woche“ und „dieses Jahr“ vom
/// selben Tag aus, an dem auch der Messbestand endet.
/// </summary>
/// <param name="now">Der feste Zeitpunkt.</param>
internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => now;

    /// <inheritdoc />
    public TimeSpan MonotonicElapsed => TimeSpan.Zero;
}
