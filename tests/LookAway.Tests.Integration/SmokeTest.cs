namespace LookAway.Tests.Integration;

/// <summary>
/// Stellt sicher, dass das Integrationstest-Projekt gebaut wird und der Test-Runner laeuft.
/// Echte Integrationstests (Dateisystem, Registry) folgen.
/// </summary>
public sealed class SmokeTest
{
    [Fact]
    public void IntegrationTestProject_BuildsAndRuns()
    {
        Assert.True(true);
    }
}
