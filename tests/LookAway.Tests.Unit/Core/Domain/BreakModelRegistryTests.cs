using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core.Domain;

/// <summary>
/// Tests fuer die in <see cref="BreakModelRegistry"/> hinterlegten Standardintervalle.
/// </summary>
public sealed class BreakModelRegistryTests
{
    [Theory]
    [InlineData(BreakModel.ShortBreaks, 60, 5)]
    [InlineData(BreakModel.ClassicPomodoro, 25, 5)]
    [InlineData(BreakModel.ModifiedPomodoro, 50, 10)]
    [InlineData(BreakModel.Ultradian, 90, 20)]
    [InlineData(BreakModel.PhysicalCounter, 40, 2)]
    [InlineData(BreakModel.LegalCompliance, 120, 15)]
    public void GetDefault_ReturnsExpectedDurations(BreakModel model, int workMinutes, int breakMinutes)
    {
        BreakInterval interval = BreakModelRegistry.GetDefault(model);

        Assert.Equal(TimeSpan.FromMinutes(workMinutes), interval.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(breakMinutes), interval.BreakDuration);
    }

    [Fact]
    public void TaskBased_HasMaxLimit()
    {
        BreakInterval interval = BreakModelRegistry.GetDefault(BreakModel.TaskBased);

        Assert.Equal(TimeSpan.FromMinutes(120), interval.MaxLimit);
    }

    [Theory]
    [InlineData(BreakModel.ShortBreaks)]
    [InlineData(BreakModel.ClassicPomodoro)]
    [InlineData(BreakModel.ModifiedPomodoro)]
    [InlineData(BreakModel.Ultradian)]
    [InlineData(BreakModel.PhysicalCounter)]
    [InlineData(BreakModel.LegalCompliance)]
    public void NonTaskBasedModels_HaveNoMaxLimit(BreakModel model)
    {
        BreakInterval interval = BreakModelRegistry.GetDefault(model);

        Assert.Null(interval.MaxLimit);
    }

    [Fact]
    public void GetDefault_RejectsUndefinedModel()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => BreakModelRegistry.GetDefault((BreakModel)999));
    }
}
