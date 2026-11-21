using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Tests;

/// <summary>
/// Was das Overlay aus den Einstellungen zeigt: Farbe, oder Bild samt Abdunklung, und still die
/// Farbe, solange „Bild“ ohne Datei gewählt ist.
/// </summary>
public sealed class OverlayBackdropTests
{
    private const string Picture = @"C:\Bilder\wald.jpg";

    [Fact]
    public void From_WithColor_HasNoImage()
    {
        Settings settings = new() { BreakOverlayColor = "#123456", BreakOverlayImagePath = Picture };

        OverlayBackdrop backdrop = OverlayBackdrop.From(settings);

        Assert.False(backdrop.HasImage);
        Assert.Null(backdrop.ImagePath);
        Assert.Equal("#123456", backdrop.ColorHex);
    }

    [Fact]
    public void From_WithImage_CarriesPathDimmingAndTheColorAsFallback()
    {
        Settings settings = new()
        {
            BreakOverlayBackground = OverlayBackground.Image,
            BreakOverlayImagePath = Picture,
            BreakOverlayImageDimPercent = 55,
            BreakOverlayColor = "#123456",
        };

        OverlayBackdrop backdrop = OverlayBackdrop.From(settings);

        Assert.True(backdrop.HasImage);
        Assert.Equal(Picture, backdrop.ImagePath);
        Assert.Equal(55, backdrop.ImageDimPercent);
        Assert.Equal("#123456", backdrop.ColorHex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void From_WithImageButNoFile_ShowsTheColor(string? path)
    {
        Settings settings = new() { BreakOverlayBackground = OverlayBackground.Image, BreakOverlayImagePath = path };

        Assert.False(OverlayBackdrop.From(settings).HasImage);
    }

    [Fact]
    public void From_WithoutSettings_Throws()
        => Assert.Throws<ArgumentNullException>(() => OverlayBackdrop.From(null!));
}
