using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für die in <see cref="BreakModelRegistry"/> hinterlegten Modelle:
/// Standardintervalle, Benutzer-Überschreibung, Anpassungsbereiche und
/// Hinweis-Schlüssel.
/// </summary>
public sealed class BreakModelRegistryTests
{
    [Theory]
    [InlineData(BreakModel.ShortBreaks, 60, 5)]
    [InlineData(BreakModel.ClassicPomodoro, 25, 5)]
    [InlineData(BreakModel.ModifiedPomodoro, 50, 10)]
    [InlineData(BreakModel.Ultradian, 90, 20)]
    [InlineData(BreakModel.PhysicalCounter, 40, 2)]
    [InlineData(BreakModel.TaskBased, 120, 10)]
    [InlineData(BreakModel.LegalCompliance, 120, 15)]
    public void GetDefault_ReturnsExpectedDurations(BreakModel model, int workMinutes, int breakMinutes)
    {
        BreakInterval interval = BreakModelRegistry.GetDefault(model);

        Assert.Equal(TimeSpan.FromMinutes(workMinutes), interval.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(breakMinutes), interval.BreakDuration);
    }

    [Fact]
    public void GetDefault_TaskBased_HasMaxLimit()
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
    public void GetDefault_NonTaskBasedModels_HaveNoMaxLimit(BreakModel model)
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

    [Fact]
    public void GetEffective_WithoutCustomDurations_ReturnsDefault()
    {
        BreakInterval effective = BreakModelRegistry.GetEffective(BreakModel.ClassicPomodoro, null);

        Assert.Equal(BreakModelRegistry.GetDefault(BreakModel.ClassicPomodoro), effective);
    }

    [Fact]
    public void GetEffective_WithCustomDurations_OverridesDefaults()
    {
        CustomDurations custom = new() { WorkMinutes = 45, BreakMinutes = 15 };

        BreakInterval effective = BreakModelRegistry.GetEffective(BreakModel.ClassicPomodoro, custom);

        Assert.Equal(TimeSpan.FromMinutes(45), effective.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(15), effective.BreakDuration);
    }

    [Fact]
    public void GetEffective_TaskBasedWithCustom_PreservesMaxLimit()
    {
        CustomDurations custom = new() { WorkMinutes = 45, BreakMinutes = 10 };

        BreakInterval effective = BreakModelRegistry.GetEffective(BreakModel.TaskBased, custom);

        Assert.Equal(TimeSpan.FromMinutes(45), effective.WorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(120), effective.MaxLimit);
    }

    [Fact]
    public void GetEffective_CustomWorkExceedingMaxLimit_Throws()
    {
        // TaskBased MaxLimit ist 120 min; eine größere Arbeitsdauer verletzt die Querbedingung.
        CustomDurations custom = new() { WorkMinutes = 130, BreakMinutes = 10 };

        _ = Assert.Throws<ArgumentException>(
            () => BreakModelRegistry.GetEffective(BreakModel.TaskBased, custom));
    }

    [Fact]
    public void GetWorkDurationRange_PhysicalCounter_Returns30To45()
    {
        WorkDurationRange? range = BreakModelRegistry.GetWorkDurationRange(BreakModel.PhysicalCounter);

        Assert.True(range.HasValue);
        WorkDurationRange value = range!.Value;
        Assert.Equal(TimeSpan.FromMinutes(30), value.Min);
        Assert.Equal(TimeSpan.FromMinutes(45), value.Max);
    }

    [Theory]
    [InlineData(BreakModel.ShortBreaks)]
    [InlineData(BreakModel.ClassicPomodoro)]
    [InlineData(BreakModel.ModifiedPomodoro)]
    [InlineData(BreakModel.Ultradian)]
    [InlineData(BreakModel.TaskBased)]
    [InlineData(BreakModel.LegalCompliance)]
    public void GetWorkDurationRange_OtherModels_ReturnNull(BreakModel model)
    {
        Assert.Null(BreakModelRegistry.GetWorkDurationRange(model));
    }

    [Theory]
    [InlineData(BreakModel.ShortBreaks, BreakHintKeys.ShortBreaks)]
    [InlineData(BreakModel.ClassicPomodoro, BreakHintKeys.Pomodoro)]
    [InlineData(BreakModel.ModifiedPomodoro, BreakHintKeys.Pomodoro)]
    [InlineData(BreakModel.Ultradian, BreakHintKeys.Ultradian)]
    [InlineData(BreakModel.PhysicalCounter, BreakHintKeys.PhysicalCounter)]
    [InlineData(BreakModel.TaskBased, BreakHintKeys.TaskBased)]
    [InlineData(BreakModel.LegalCompliance, BreakHintKeys.LegalCompliance)]
    public void GetHintKey_ReturnsExpectedKey(BreakModel model, string expectedKey)
    {
        Assert.Equal(expectedKey, BreakModelRegistry.GetHintKey(model));
    }

    [Fact]
    public void GetHintKey_RejectsUndefinedModel()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => BreakModelRegistry.GetHintKey((BreakModel)999));
    }
}
