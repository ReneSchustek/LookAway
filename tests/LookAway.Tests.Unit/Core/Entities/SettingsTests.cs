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
        Assert.Null(settings.CustomDurations);
        Assert.False(settings.IsFirstRun);
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
