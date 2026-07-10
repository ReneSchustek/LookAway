using LookAway.Core.Interfaces;

namespace LookAway.Core.Tests.Fakes;

/// <summary>
/// Test-Fake für <see cref="IIdleDetector"/>. <see cref="IdleTime"/> ist frei
/// setzbar, um Inaktivität deterministisch zu simulieren.
/// </summary>
internal sealed class FakeIdleDetector : IIdleDetector
{
    /// <summary>Die zurückzugebende Inaktivitätsdauer.</summary>
    public TimeSpan IdleTime { get; set; }

    public TimeSpan GetIdleTime() => IdleTime;
}
