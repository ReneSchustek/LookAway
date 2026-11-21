using LookAway.Core.Domain;

namespace LookAway.Core.Tests;

/// <summary>
/// Die Befehlszeile, mit der LookAway das Setup einer Aktualisierung startet.
/// </summary>
public sealed class InstallerArgumentsTests
{
    /// <remarks>Ohne „/RELAUNCH“ bliebe LookAway nach einem Update beendet.</remarks>
    [Fact]
    public void SilentUpdate_RunsWithoutQuestionsAndRestartsLookAway()
        => Assert.Equal(["/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", InstallerArguments.Relaunch], InstallerArguments.SilentUpdate);
}
