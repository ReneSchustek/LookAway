namespace LookAway.Tests.Unit;

/// <summary>
/// Stellt sicher, dass das Test-Projekt gebaut, geladen und xUnit korrekt verkabelt ist.
/// Wird mit der ersten echten Domain-Logik durch fachliche Tests ersetzt oder ergaenzt.
/// </summary>
public sealed class SmokeTest
{
    [Fact]
    public void Solution_BuildsAndTestRunnerWorks()
    {
        const int expected = 42;
        int actual = 40 + 2;
        Assert.Equal(expected, actual);
    }
}
