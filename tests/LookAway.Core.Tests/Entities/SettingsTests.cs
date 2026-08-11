using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für die Default-Werte und die Setter-Validierung der
/// <see cref="Settings"/>-Entität.
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
        Assert.True(settings.AutoStartBreakEnabled);
        Assert.Equal(15, settings.AutoStartBreakSeconds);
        Assert.Null(settings.CustomDurations);
        Assert.False(settings.IsFirstRun);
    }

    [Theory]
    [InlineData(Settings.MinAutoStartBreakSeconds)]
    [InlineData(30)]
    [InlineData(Settings.MaxAutoStartBreakSeconds)]
    public void AutoStartBreakSeconds_AcceptsBoundaryValues(int seconds)
    {
        Settings settings = new() { AutoStartBreakSeconds = seconds };
        Assert.Equal(seconds, settings.AutoStartBreakSeconds);
    }

    [Theory]
    [InlineData(Settings.MinAutoStartBreakSeconds - 1)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Settings.MaxAutoStartBreakSeconds + 1)]
    public void AutoStartBreakSeconds_ThrowsOnOutOfRange(int seconds)
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.AutoStartBreakSeconds = seconds);
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

    [Fact]
    public void OverlayDefaults_AreSetOnFreshInstance()
    {
        Settings settings = new();

        Assert.True(settings.DarkenAllScreens);
        Assert.Equal(Settings.DefaultBreakOverlayColor, settings.BreakOverlayColor);
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#F20F1115")]
    [InlineData("#abcdef")]
    public void BreakOverlayColor_AcceptsValidHex(string color)
    {
        Settings settings = new() { BreakOverlayColor = color };
        Assert.Equal(color, settings.BreakOverlayColor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("F20F1115")]
    [InlineData("#FFF")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#F20F11150")]
    public void BreakOverlayColor_ThrowsOnInvalidHex(string color)
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentException>(
            () => settings.BreakOverlayColor = color);
    }

    [Fact]
    public void DarkenAllScreens_CanBeToggled()
    {
        Settings settings = new() { DarkenAllScreens = false };
        Assert.False(settings.DarkenAllScreens);

        settings.DarkenAllScreens = true;
        Assert.True(settings.DarkenAllScreens);
    }

    [Fact]
    public void AutoUpdate_DefaultsToFalseAndCanBeToggled()
    {
        Settings settings = new();
        Assert.False(settings.AutoUpdate);

        settings.AutoUpdate = true;
        Assert.True(settings.AutoUpdate);
    }

    [Theory]
    [InlineData(AppTheme.System)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void AppTheme_AcceptsAllDefinedValues(AppTheme theme)
    {
        Settings settings = new() { AppTheme = theme };
        Assert.Equal(theme, settings.AppTheme);
    }

    /// <remarks>
    /// Die Werte stammen aus der abgelegten Datei und können jeden Zahlenwert
    /// tragen, den jemand hineinschreibt — auch einen, den es nie gab.
    /// </remarks>
    [Fact]
    public void AppTheme_ThrowsOnUndefinedValue()
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.AppTheme = (AppTheme)99);
    }

    [Fact]
    public void ReminderSound_ThrowsOnUndefinedValue()
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.ReminderSound = (SoundType)99);
    }

    [Theory]
    [InlineData(Settings.MinSoundVolumePercent)]
    [InlineData(50)]
    [InlineData(Settings.MaxSoundVolumePercent)]
    public void SoundVolumePercent_AcceptsBoundaryValues(int percent)
    {
        Settings settings = new() { SoundVolumePercent = percent };
        Assert.Equal(percent, settings.SoundVolumePercent);
    }

    [Theory]
    [InlineData(Settings.MinSoundVolumePercent - 1)]
    [InlineData(Settings.MaxSoundVolumePercent + 1)]
    [InlineData(-100)]
    public void SoundVolumePercent_ThrowsOnOutOfRange(int percent)
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.SoundVolumePercent = percent);
    }

    [Fact]
    public void UpdateCheckFrequency_ThrowsOnUndefinedValue()
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.UpdateCheckFrequency = (UpdateCheckFrequency)99);
    }

    [Theory]
    [InlineData(Settings.MinDimBrightnessPercent)]
    [InlineData(50)]
    [InlineData(Settings.MaxDimBrightnessPercent)]
    public void DimBrightnessPercent_AcceptsBoundaryValues(int percent)
    {
        Settings settings = new() { DimBrightnessPercent = percent };
        Assert.Equal(percent, settings.DimBrightnessPercent);
    }

    /// <remarks>
    /// Die untere Grenze ist bewusst nicht null: Ein vollständig schwarzer Bildschirm
    /// wäre von einem defekten Monitor nicht zu unterscheiden.
    /// </remarks>
    [Theory]
    [InlineData(Settings.MinDimBrightnessPercent - 1)]
    [InlineData(0)]
    [InlineData(Settings.MaxDimBrightnessPercent + 1)]
    public void DimBrightnessPercent_ThrowsOnOutOfRange(int percent)
    {
        Settings settings = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.DimBrightnessPercent = percent);
    }
}
