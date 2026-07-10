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
    public void IsLight_DistinguishesDarkAndLight()
    {
        Assert.True(HexColor.IsLight(0xFF, 0xFF, 0xFF));
        Assert.False(HexColor.IsLight(0x0F, 0x11, 0x15));
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
