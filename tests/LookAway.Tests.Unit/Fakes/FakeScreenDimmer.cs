using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>Test-Fake für <see cref="IScreenDimmer"/>: merkt sich die Aufrufe.</summary>
internal sealed class FakeScreenDimmer : IScreenDimmer
{
    /// <summary>Anzahl der <see cref="DimTo"/>-Aufrufe.</summary>
    public int DimCallCount { get; private set; }

    /// <summary>Anzahl der <see cref="Restore"/>-Aufrufe.</summary>
    public int RestoreCallCount { get; private set; }

    /// <summary>Zuletzt angeforderte Zielhelligkeit.</summary>
    public int LastTargetPercent { get; private set; }

    /// <inheritdoc />
    public void DimTo(int targetPercent)
    {
        DimCallCount++;
        LastTargetPercent = targetPercent;
    }

    /// <inheritdoc />
    public void Restore() => RestoreCallCount++;
}
