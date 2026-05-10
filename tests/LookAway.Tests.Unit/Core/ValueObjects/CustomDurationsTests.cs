using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core.ValueObjects;

/// <summary>
/// Tests fuer die Wertebereichs-Validierung des <see cref="CustomDurations"/>-Value-Objects.
/// </summary>
public sealed class CustomDurationsTests
{
    [Fact]
    public void Constructor_AcceptsValuesInRange()
    {
        CustomDurations durations = new() { WorkMinutes = 25, BreakMinutes = 5 };

        Assert.Equal(25, durations.WorkMinutes);
        Assert.Equal(5, durations.BreakMinutes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(CustomDurations.MaxWorkMinutes)]
    public void WorkMinutes_AcceptsBoundaryValues(int minutes)
    {
        CustomDurations durations = new() { WorkMinutes = minutes, BreakMinutes = 5 };
        Assert.Equal(minutes, durations.WorkMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(CustomDurations.MaxWorkMinutes + 1)]
    public void WorkMinutes_ThrowsOnOutOfRange(int minutes)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CustomDurations { WorkMinutes = minutes, BreakMinutes = 5 });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(CustomDurations.MaxBreakMinutes)]
    public void BreakMinutes_AcceptsBoundaryValues(int minutes)
    {
        CustomDurations durations = new() { WorkMinutes = 25, BreakMinutes = minutes };
        Assert.Equal(minutes, durations.BreakMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CustomDurations.MaxBreakMinutes + 1)]
    public void BreakMinutes_ThrowsOnOutOfRange(int minutes)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CustomDurations { WorkMinutes = 25, BreakMinutes = minutes });
    }

    [Fact]
    public void Records_WithSameValuesAreEqual()
    {
        CustomDurations a = new() { WorkMinutes = 50, BreakMinutes = 10 };
        CustomDurations b = new() { WorkMinutes = 50, BreakMinutes = 10 };

        Assert.Equal(a, b);
    }
}
