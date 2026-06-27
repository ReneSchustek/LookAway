using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// Test-Fake fuer <see cref="IFullscreenDetector"/>. <see cref="IsActive"/>
/// steuert das Erkennungsergebnis deterministisch.
/// </summary>
internal sealed class FakeFullscreenDetector : IFullscreenDetector
{
    /// <summary>Soll eine Vollbild-App gemeldet werden?</summary>
    public bool IsActive { get; set; }

    public bool IsFullscreenApplicationActive() => IsActive;
}
