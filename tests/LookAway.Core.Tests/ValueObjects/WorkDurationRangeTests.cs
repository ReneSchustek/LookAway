using LookAway.Core.ValueObjects;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für das <see cref="WorkDurationRange"/>-ValueObject.
/// </summary>
public sealed class WorkDurationRangeTests
{
    [Fact]
    public void Constructor_AcceptsValidRange()
    {
        WorkDurationRange range = new(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(45));

        Assert.Equal(TimeSpan.FromMinutes(30), range.Min);
        Assert.Equal(TimeSpan.FromMinutes(45), range.Max);
    }

    [Fact]
    public void Constructor_AcceptsEqualMinAndMax()
    {
        WorkDurationRange range = new(TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(40));

        Assert.Equal(range.Min, range.Max);
    }

    [Fact]
    public void Constructor_NonPositiveMin_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkDurationRange(TimeSpan.Zero, TimeSpan.FromMinutes(45)));
    }

    [Fact]
    public void Constructor_MinGreaterThanMax_Throws()
    {
        _ = Assert.Throws<ArgumentException>(
            () => new WorkDurationRange(TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(45)));
    }
}
