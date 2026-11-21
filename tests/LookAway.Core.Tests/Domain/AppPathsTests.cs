using System.IO;
using LookAway.Core.Domain;

namespace LookAway.Core.Tests;

/// <summary>
/// Die Datenverzeichnis-Auflösung: portabel neben der EXE, sonst unter <c>%APPDATA%</c>.
/// Vertauscht, fände eine Installation ihre Einstellungen nicht wieder und begänne beim
/// nächsten Start mit dem Einrichtungsassistenten.
/// </summary>
public sealed class AppPathsTests
{
    private const string BaseDir = @"C:\Programme\LookAway";
    private const string AppData = @"C:\Users\test\AppData\Roaming";

    [Fact]
    public void Portable_PlacesDataNextToTheExecutable()
    {
        string directory = AppPaths.ResolveDataDirectory(isPortable: true, BaseDir, AppData);

        Assert.Equal(BaseDir, directory);
    }

    [Fact]
    public void NonPortable_UsesAppDataFolder()
    {
        string directory = AppPaths.ResolveDataDirectory(isPortable: false, BaseDir, AppData);

        Assert.Equal(Path.Combine(AppData, AppPaths.AppFolderName), directory);
    }
}
