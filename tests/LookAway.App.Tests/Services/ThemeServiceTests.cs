using LookAway.App.Services;
using LookAway.Core.Enums;
using Microsoft.UI.Xaml;

namespace LookAway.App.Tests;

/// <summary>
/// Das Erscheinungsbild: Die Wahl wird nur übernommen, wenn es sie gibt, und jede Wahl hat ihre
/// Entsprechung bei den WinUI-Elementen.
/// </summary>
public sealed class ThemeServiceTests
{
    [Fact]
    public void Starts_WithTheSystemSetting()
        => Assert.Equal(AppTheme.System, new ThemeService().Current);

    [Fact]
    public void SetTheme_KeepsTheChoice()
    {
        ThemeService service = new();

        service.SetTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, service.Current);
    }

    [Fact]
    public void SetTheme_RejectsAnUnknownValue()
    {
        ThemeService service = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => service.SetTheme((AppTheme)42));
        Assert.Equal(AppTheme.System, service.Current);
    }

    [Theory]
    [InlineData(AppTheme.Light, ElementTheme.Light)]
    [InlineData(AppTheme.Dark, ElementTheme.Dark)]
    [InlineData(AppTheme.System, ElementTheme.Default)]
    public void EveryChoice_MapsToAnElementTheme(AppTheme theme, ElementTheme expected)
        => Assert.Equal(expected, ThemeService.ToElementTheme(theme));
}
