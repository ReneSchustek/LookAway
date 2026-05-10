using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core.ValueObjects;

/// <summary>
/// Tests fuer das <see cref="BreakInterval"/>-ValueObject.
/// </summary>
public sealed class BreakIntervalTests
{
    [Fact]
    public void Construction_AcceptsValidValues()
    {
        BreakInterval interval = new()
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromMinutes(5),
        };

        Assert.Equal(TimeSpan.FromMinutes(25), interval.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), interval.BreakDuration);
        Assert.Null(interval.MaxLimit);
    }

    [Fact]
    public void WorkDuration_BelowMinimum_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BreakInterval
        {
            WorkDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromMinutes(5),
        });
    }

    [Fact]
    public void WorkDuration_AboveMaximum_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BreakInterval
        {
            WorkDuration = TimeSpan.FromHours(9),
            BreakDuration = TimeSpan.FromMinutes(5),
        });
    }

    [Fact]
    public void BreakDuration_BelowMinimum_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromSeconds(30),
        });
    }

    [Fact]
    public void BreakDuration_AboveMaximum_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromHours(3),
        });
    }

    [Fact]
    public void MaxLimit_AcceptsNull()
    {
        BreakInterval interval = new()
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromMinutes(5),
            MaxLimit = null,
        };

        Assert.Null(interval.MaxLimit);
    }

    [Fact]
    public void MaxLimit_OutsideRange_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromMinutes(5),
            MaxLimit = TimeSpan.FromHours(10),
        });
    }

    [Fact]
    public void Records_WithSameValuesAreEqual()
    {
        BreakInterval a = new()
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromMinutes(5),
        };
        BreakInterval b = new()
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromMinutes(5),
        };

        Assert.Equal(a, b);
    }
}
