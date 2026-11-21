using LookAway.Core.Domain;

namespace LookAway.Core.Tests;

/// <summary>
/// Regeln für ein eigenes Overlay-Bild: unterstützte Formate, die Abdunklung als Grundlage der
/// Schriftfarbe und die Dekodiergröße je Monitor.
/// </summary>
public sealed class OverlayImageTests
{
    [Theory]
    [InlineData(@"C:\Bilder\wald.jpg")]
    [InlineData(@"C:\Bilder\wald.JPEG")]
    [InlineData(@"C:\Bilder\wald.Png")]
    [InlineData("wald.jpeg")]
    public void IsSupported_AcceptsJpgAndPngInAnyCase(string path)
        => Assert.True(OverlayImage.IsSupported(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Bilder\wald.gif")]
    [InlineData(@"C:\Bilder\wald.webp")]
    [InlineData(@"C:\Bilder\wald")]
    [InlineData(@"C:\Bilder\wald.jpg.exe")]
    public void IsSupported_RejectsEverythingElse(string? path)
        => Assert.False(OverlayImage.IsSupported(path));

    [Fact]
    public void Dim_WithoutDimming_KeepsTheColor()
        => Assert.Equal(((byte)200, (byte)150, (byte)100), OverlayImage.Dim((200, 150, 100), 0));

    /// <remarks>Schwarz mit 40 % Deckkraft lässt 60 % jedes Kanals übrig.</remarks>
    [Fact]
    public void Dim_DarkensEveryChannelByThePercentage()
        => Assert.Equal(((byte)120, (byte)90, (byte)60), OverlayImage.Dim((200, 150, 100), 40));

    /// <remarks>255 × 0,25 = 63,75 — gerundet, nicht abgeschnitten.</remarks>
    [Fact]
    public void Dim_RoundsToTheNearestValue()
        => Assert.Equal(((byte)64, (byte)64, (byte)64), OverlayImage.Dim((255, 255, 255), 75));

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Dim_RejectsPercentagesOutsideTheRange(int percent)
        => Assert.Throws<ArgumentOutOfRangeException>(() => OverlayImage.Dim((1, 2, 3), percent));

    /// <remarks>Querformat auf Querformat: Die Breite des Monitors bestimmt die Größe.</remarks>
    [Fact]
    public void DecodeWidth_ForALandscapeMonitor_MatchesTheMonitorWidth()
        => Assert.Equal(2560, OverlayImage.DecodeWidth(6000, 4000, 2560, 1080));

    /// <remarks>
    /// Querformat auf Hochkant-Monitor: Die Höhe von 1920 muss gefüllt werden, also
    /// 6000 × 1920 / 4000 = 2880 Pixel Breite, von denen die Ränder beschnitten werden.
    /// </remarks>
    [Fact]
    public void DecodeWidth_ForAPortraitMonitor_FillsTheHeight()
        => Assert.Equal(2880, OverlayImage.DecodeWidth(6000, 4000, 1080, 1920));

    [Fact]
    public void DecodeWidth_ForASmallImage_NeverDecodesLargerThanTheImage()
        => Assert.Equal(800, OverlayImage.DecodeWidth(800, 600, 2560, 1440));

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, -1)]
    public void DecodeWidth_RejectsEmptySizes(int imageWidth, int imageHeight, int areaWidth, int areaHeight)
        => Assert.Throws<ArgumentOutOfRangeException>(() => OverlayImage.DecodeWidth(imageWidth, imageHeight, areaWidth, areaHeight));
}
