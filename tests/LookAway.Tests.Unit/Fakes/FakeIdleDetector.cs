using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// Test-Fake fuer <see cref="IIdleDetector"/>. <see cref="IdleTime"/> ist frei
/// setzbar, um Inaktivitaet deterministisch zu simulieren.
/// </summary>
internal sealed class FakeIdleDetector : IIdleDetector
{
    /// <summary>Die zurueckzugebende Inaktivitaetsdauer.</summary>
    public TimeSpan IdleTime { get; set; }

    public TimeSpan GetIdleTime() => IdleTime;
}
