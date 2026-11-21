using LookAway.Data.Time;

namespace LookAway.Data.Tests;

/// <summary>
/// Die echte Uhr: Wanduhrzeit in UTC und eine Laufzeit, die nur vorwärts geht. Geprüft wird gegen
/// Grenzen, die vor und nach dem Ablesen genommen werden, nicht gegen eine feste Zeit.
/// </summary>
public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_LiesBetweenTwoReadingsOfTheSystemClockInUtc()
    {
        SystemClock clock = new();

        DateTimeOffset before = DateTimeOffset.UtcNow;
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset after = DateTimeOffset.UtcNow;

        Assert.InRange(now, before, after);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }

    [Fact]
    public void MonotonicElapsed_NeverRunsBackwards()
    {
        SystemClock clock = new();

        TimeSpan first = clock.MonotonicElapsed;
        TimeSpan second = clock.MonotonicElapsed;

        Assert.True(second >= first);
    }
}
