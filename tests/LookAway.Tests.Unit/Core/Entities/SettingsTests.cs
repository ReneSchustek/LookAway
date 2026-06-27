using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core.Entities;

/// <summary>
/// Tests fuer die Default-Werte und die Setter-Validierung der
/// <see cref="Settings"/>-Entitaet.
/// </summary>
public sealed class SettingsTests
{
    [Fact]
    public void Defaults_AreSetOnFreshInstance()
    {
        Settings settings = new();

        Assert.Equal(Language.German, settings.Language);
        Assert.Equal(BreakModel.ClassicPomodoro, settings.BreakModel);
        Assert.False(settings.AutoStart);
        Assert.True(settings.PauseOnIdle);
        Assert.Equal(5, settings.IdleThresholdMinutes);
        Assert.True(settings.SuppressOnFullscreen);
        Assert.Null(settings.CustomDurations);
        Assert.False(settings.IsFirstRun);
    }

    [Theory]
    [InlineData(Settings.MinIdleThresholdMinutes)]
    [InlineData(5)]
    [InlineData(Settings.MaxIdleThresholdMinutes)]
    public void IdleThresholdMinutes_AcceptsBoundaryValues(int minutes)
    {
        Settings settings = new() { IdleThresholdMinutes = minutes };
        Assert.Equal(minutes, settings.IdleThresholdMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Settings.MaxIdleThresholdMinutes + 1)]
    public void IdleThresholdMinutes_ThrowsOnOutOfRange(int minutes)
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.IdleThresholdMinutes = minutes);
    }

    [Fact]
    public void DetectionFlags_CanBeToggled()
    {
        Settings settings = new() { PauseOnIdle = false, SuppressOnFullscreen = false };

        Assert.False(settings.PauseOnIdle);
        Assert.False(settings.SuppressOnFullscreen);
    }

    [Fact]
    public void Language_AcceptsAllDefinedValues()
    {
        Settings settings = new();

        foreach (Language language in Enum.GetValues<Language>())
        {
            settings.Language = language;
            Assert.Equal(language, settings.Language);
        }
    }

    [Fact]
    public void Language_ThrowsOnUndefinedValue()
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.Language = (Language)999);
    }

    [Fact]
    public void BreakModel_AcceptsAllDefinedValues()
    {
        Settings settings = new();

        foreach (BreakModel model in Enum.GetValues<BreakModel>())
        {
            settings.BreakModel = model;
            Assert.Equal(model, settings.BreakModel);
        }
    }

    [Fact]
    public void BreakModel_ThrowsOnUndefinedValue()
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.BreakModel = (BreakModel)999);
    }

    [Fact]
    public void CustomDurations_DefaultsToNullAndAcceptsAssignment()
    {
        Settings settings = new();
        Assert.Null(settings.CustomDurations);

        CustomDurations custom = new() { WorkMinutes = 30, BreakMinutes = 5 };
        settings.CustomDurations = custom;

        Assert.Same(custom, settings.CustomDurations);
    }

    [Fact]
    public void AutoStart_CanBeToggled()
    {
        Settings settings = new() { AutoStart = true };
        Assert.True(settings.AutoStart);

        settings.AutoStart = false;
        Assert.False(settings.AutoStart);
    }
}
