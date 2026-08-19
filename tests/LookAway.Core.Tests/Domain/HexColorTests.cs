using LookAway.Core.Domain;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für die ARGB-Hex-Hilfslogik <see cref="HexColor"/> (Validierung,
/// Parsing, Formatierung, Helligkeitsentscheidung).
/// </summary>
public sealed class HexColorTests
{
    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#F20F1115")]
    [InlineData("#abcdef")]
    public void IsValid_AcceptsValidHex(string value) => Assert.True(HexColor.IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("F20F1115")]
    [InlineData("#FFF")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#F20F11150")]
    public void IsValid_RejectsInvalidHex(string? value) => Assert.False(HexColor.IsValid(value));

    [Fact]
    public void TryParse_SixDigits_DefaultsAlphaToOpaque()
    {
        Assert.True(HexColor.TryParse("#102030", out (byte A, byte R, byte G, byte B) c));
        Assert.Equal((byte)0xFF, c.A);
        Assert.Equal((byte)0x10, c.R);
        Assert.Equal((byte)0x20, c.G);
        Assert.Equal((byte)0x30, c.B);
    }

    [Fact]
    public void TryParse_EightDigits_ReadsAlpha()
    {
        Assert.True(HexColor.TryParse("#80123456", out (byte A, byte R, byte G, byte B) c));
        Assert.Equal((byte)0x80, c.A);
        Assert.Equal((byte)0x12, c.R);
        Assert.Equal((byte)0x34, c.G);
        Assert.Equal((byte)0x56, c.B);
    }

    [Fact]
    public void ParseOrDefault_InvalidInput_FallsBackToDefault()
    {
        (byte A, byte R, byte G, byte B) c = HexColor.ParseOrDefault("not-a-color");
        Assert.Equal(HexColor.DefaultComponents, c);
    }

    [Fact]
    public void ToHex_RoundTripsWithTryParse()
    {
        string hex = HexColor.ToHex(0x80, 0x12, 0x34, 0x56);
        Assert.Equal("#80123456", hex);

        Assert.True(HexColor.TryParse(hex, out (byte A, byte R, byte G, byte B) c));
        Assert.Equal(((byte)0x80, (byte)0x12, (byte)0x34, (byte)0x56), c);
    }

    [Fact]
    public void ContrastRatio_ReachesTheExtremesOfTheScale()
    {
        Assert.Equal(21.0, HexColor.ContrastRatio((0x00, 0x00, 0x00), (0xFF, 0xFF, 0xFF)), 3);
        Assert.Equal(1.0, HexColor.ContrastRatio((0x12, 0x34, 0x56), (0x12, 0x34, 0x56)), 3);
    }

    [Fact]
    public void ContrastRatio_IsIndependentOfArgumentOrder()
    {
        Assert.Equal(
            HexColor.ContrastRatio((0x9E, 0x9E, 0x9E), (0x0B, 0x1F, 0x1C)),
            HexColor.ContrastRatio((0x0B, 0x1F, 0x1C), (0x9E, 0x9E, 0x9E)),
            10);
    }

    /// <remarks>
    /// Das mittlere Grau, das eine halbtransparente Farbe ergibt, ist der Fall, an dem
    /// eine Helligkeitsschwelle danebenlag: Sie erklärte es für dunkel und wählte helle
    /// Schrift. Gemessen liest sich dunkle Schrift dort mehr als doppelt so gut.
    /// </remarks>
    [Fact]
    public void ContrastRatio_PrefersDarkInkOnMediumGrey()
    {
        (byte R, byte G, byte B) grey = (0x9E, 0x9E, 0x9E);

        double dark = HexColor.ContrastRatio(grey, (0x0B, 0x1F, 0x1C));
        double light = HexColor.ContrastRatio(grey, (0xFF, 0xFF, 0xFF));

        Assert.True(dark > light, $"Dunkel {dark:F2}:1 muss heller Schrift {light:F2}:1 vorgehen.");
    }

    [Theory]
    // Sehr dunkle und sehr helle Flächen bleiben eindeutig — hier darf die Wahl nicht kippen.
    [InlineData(0x0F, 0x11, 0x15, false)]
    [InlineData(0xEF, 0xEF, 0xEF, true)]
    [InlineData(0x9E, 0x9E, 0x9E, true)]
    public void ContrastRatio_ChoosesTheReadableInk(byte r, byte g, byte b, bool expectsDarkInk)
    {
        double dark = HexColor.ContrastRatio((r, g, b), (0x0B, 0x1F, 0x1C));
        double light = HexColor.ContrastRatio((r, g, b), (0xFF, 0xFF, 0xFF));

        Assert.Equal(expectsDarkInk, dark > light);
    }

    [Fact]
    public void FlattenOverWhite_OnComponents_MatchesTheHexVariant()
    {
        Assert.Equal(((byte)0x9E, (byte)0x9E, (byte)0x9E), HexColor.FlattenOverWhite((0x61, 0x00, 0x00, 0x00)));
        Assert.Equal(((byte)0x12, (byte)0x34, (byte)0x56), HexColor.FlattenOverWhite((0xFF, 0x12, 0x34, 0x56)));
    }

    [Fact]
    public void FlattenOverWhite_CompositesSemiTransparentBlackToGrey()
    {
        // Schwarz mit 0x61 (97) Deckkraft über Weiß ergibt Grau: 255*(255-97)/255 = 158 = 0x9E.
        Assert.Equal("#FF9E9E9E", HexColor.FlattenOverWhite("#61000000"));
    }

    [Fact]
    public void FlattenOverWhite_LeavesOpaqueColorsUnchanged()
    {
        Assert.Equal("#FF123456", HexColor.FlattenOverWhite("#FF123456"));
        // 6-stellige Eingabe gilt als deckend.
        Assert.Equal("#FF123456", HexColor.FlattenOverWhite("#123456"));
    }
}
