using LookAway.App.Converters;
using Microsoft.UI.Xaml;

namespace LookAway.App.Tests;

/// <summary>
/// Die Wertumwandler der Oberfläche: Sichtbarkeit aus Wahrheitswerten und Großschreibung nach der
/// Sprache der Anzeige.
/// </summary>
public sealed class ConverterTests
{
    [Fact]
    public void BoolToVisibility_ShowsOnlyForTrue()
    {
        BoolToVisibilityConverter converter = new();

        Assert.Equal(Visibility.Visible, converter.Convert(true, typeof(Visibility), null!, string.Empty));
        Assert.Equal(Visibility.Collapsed, converter.Convert(false, typeof(Visibility), null!, string.Empty));
        Assert.Equal(Visibility.Collapsed, converter.Convert("ja", typeof(Visibility), null!, string.Empty));
        Assert.Equal(true, converter.ConvertBack(Visibility.Visible, typeof(bool), null!, string.Empty));
        Assert.Equal(false, converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, string.Empty));
    }

    [Fact]
    public void InverseBoolToVisibility_HidesOnlyForTrue()
    {
        InverseBoolToVisibilityConverter converter = new();

        Assert.Equal(Visibility.Collapsed, converter.Convert(true, typeof(Visibility), null!, string.Empty));
        Assert.Equal(Visibility.Visible, converter.Convert(false, typeof(Visibility), null!, string.Empty));
        Assert.Equal(false, converter.ConvertBack(Visibility.Visible, typeof(bool), null!, string.Empty));
        Assert.Equal(true, converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, string.Empty));
    }

    /// <remarks>Ein türkisches „i“ wird zu „İ“, ein deutsches zu „I“; die Sprache der Anzeige entscheidet.</remarks>
    [Fact]
    public void UpperCase_FollowsTheLanguage()
    {
        UpperCaseConverter converter = new();

        Assert.Equal("İ", converter.Convert("i", typeof(string), null!, "tr-TR"));
        Assert.Equal("I", converter.Convert("i", typeof(string), null!, "de-DE"));
    }

    [Fact]
    public void UpperCase_WithoutLanguage_UsesTheUserCulture()
        => Assert.Equal("PAUSE", new UpperCaseConverter().Convert("Pause", typeof(string), null!, string.Empty));

    [Fact]
    public void UpperCase_PassesOtherValuesThrough()
        => Assert.Equal(42, new UpperCaseConverter().Convert(42, typeof(string), null!, string.Empty));

    [Fact]
    public void UpperCase_CannotBeUndone()
        => Assert.Throws<NotSupportedException>(() => new UpperCaseConverter().ConvertBack("PAUSE", typeof(string), null!, string.Empty));
}
