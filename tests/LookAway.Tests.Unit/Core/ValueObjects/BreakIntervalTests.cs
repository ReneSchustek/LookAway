using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core.ValueObjects;

/// <summary>
/// Tests für das <see cref="BreakInterval"/>-ValueObject: Wertebereiche,
/// Querbedingung <c>MaxLimit ≥ WorkDuration</c> und Value-Equality.
/// </summary>
public sealed class BreakIntervalTests
{
    [Fact]
    public void Create_AcceptsValidValues()
    {
        BreakInterval interval = BreakInterval.Create(
            TimeSpan.FromMinutes(25),
            TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(25), interval.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), interval.BreakDuration);
        Assert.Null(interval.MaxLimit);
    }

    [Fact]
    public void Create_AcceptsWorkAtFiveMinuteMinimum()
    {
        BreakInterval interval = BreakInterval.Create(
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromMinutes(5), interval.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(1), interval.BreakDuration);
    }

    [Fact]
    public void Create_WorkBelowFiveMinutes_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BreakInterval.Create(
            TimeSpan.FromMinutes(4),
            TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Create_WorkAboveMaximum_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BreakInterval.Create(
            TimeSpan.FromHours(9),
            TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Create_BreakBelowMinimum_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BreakInterval.Create(
            TimeSpan.FromMinutes(25),
            TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Create_BreakAboveMaximum_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BreakInterval.Create(
            TimeSpan.FromMinutes(25),
            TimeSpan.FromHours(3)));
    }

    [Fact]
    public void Create_AcceptsNullMaxLimit()
    {
        BreakInterval interval = BreakInterval.Create(
            TimeSpan.FromMinutes(25),
            TimeSpan.FromMinutes(5),
            maxLimit: null);

        Assert.Null(interval.MaxLimit);
    }

    [Fact]
    public void Create_MaxLimitEqualToWork_IsAccepted()
    {
        BreakInterval interval = BreakInterval.Create(
            TimeSpan.FromMinutes(120),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(120));

        Assert.Equal(TimeSpan.FromMinutes(120), interval.MaxLimit);
    }

    [Fact]
    public void Create_MaxLimitOutsideRange_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BreakInterval.Create(
            TimeSpan.FromMinutes(25),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromHours(10)));
    }

    [Fact]
    public void Create_MaxLimitBelowWork_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => BreakInterval.Create(
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30)));

        Assert.Equal("maxLimit", exception.ParamName);
    }

    [Fact]
    public void Records_WithSameValuesAreEqual()
    {
        BreakInterval a = BreakInterval.Create(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5));
        BreakInterval b = BreakInterval.Create(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5));

        Assert.Equal(a, b);
    }
}
