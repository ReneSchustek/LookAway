using LookAway.App.Services;
using LookAway.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Der Erinnerungston ohne Abspielen: welche Datei zu welchem Ton gehört und dass ein stummer oder
/// entsorgter Dienst nichts versucht. Abgespielt wird in keinem Test.
/// </summary>
public sealed class SoundServiceTests
{
    [Theory]
    [InlineData(SoundType.Chime, "chime.wav")]
    [InlineData(SoundType.Bell, "bell.wav")]
    [InlineData(SoundType.Pop, "pop.wav")]
    [InlineData((SoundType)42, "chime.wav")]
    public void EverySound_HasAFile(SoundType sound, string fileName)
        => Assert.Equal(fileName, SoundService.GetFileName(sound));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Play_WithoutVolume_IsSilent(int volumePercent)
    {
        using SoundService service = new(NullLogger<SoundService>.Instance);

        Exception? thrown = Record.Exception(() => service.Play(SoundType.Chime, volumePercent));

        Assert.Null(thrown);
    }

    [Fact]
    public void Play_AfterDispose_DoesNothing()
    {
        SoundService service = new(NullLogger<SoundService>.Instance);
        service.Dispose();
        service.Dispose();

        Exception? thrown = Record.Exception(() => service.Play(SoundType.Chime, 80));

        Assert.Null(thrown);
    }
}
