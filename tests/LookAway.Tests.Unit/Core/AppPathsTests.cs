using System.IO;
using LookAway.Core.Domain;

namespace LookAway.Tests.Unit.Core;

/// <summary>
/// Tests fuer die Datenverzeichnis-Aufloesung (Portable-Modus, BRIEF022).
/// </summary>
public sealed class AppPathsTests
{
    private const string BaseDir = @"C:\Programme\LookAway";
    private const string AppData = @"C:\Users\test\AppData\Roaming";

    [Fact]
    public void Portable_legt_Daten_neben_die_Exe()
    {
        string directory = AppPaths.ResolveDataDirectory(isPortable: true, BaseDir, AppData);

        Assert.Equal(BaseDir, directory);
    }

    [Fact]
    public void Nicht_portable_nutzt_AppData_LookAway()
    {
        string directory = AppPaths.ResolveDataDirectory(isPortable: false, BaseDir, AppData);

        Assert.Equal(Path.Combine(AppData, AppPaths.AppFolderName), directory);
    }
}
