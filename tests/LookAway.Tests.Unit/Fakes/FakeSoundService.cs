using LookAway.Core.Enums;
using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// Test-Fake fuer <see cref="ISoundService"/>: spielt keinen echten Ton, sondern
/// merkt sich den letzten Aufruf und zaehlt die Wiedergaben.
/// </summary>
internal sealed class FakeSoundService : ISoundService
{
    /// <summary>Anzahl der <see cref="Play"/>-Aufrufe.</summary>
    public int PlayCallCount { get; private set; }

    /// <summary>Zuletzt abgespielter Ton.</summary>
    public SoundType? LastSound { get; private set; }

    /// <summary>Zuletzt verwendete Lautstaerke.</summary>
    public int LastVolume { get; private set; }

    /// <inheritdoc />
    public void Play(SoundType soundType, int volumePercent)
    {
        PlayCallCount++;
        LastSound = soundType;
        LastVolume = volumePercent;
    }
}
