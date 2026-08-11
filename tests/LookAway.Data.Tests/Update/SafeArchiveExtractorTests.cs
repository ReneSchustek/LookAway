using System.IO.Compression;
using System.Text;
using LookAway.Core.Domain;
using LookAway.Data.Update;

namespace LookAway.Data.Tests;

/// <summary>
/// Tests für <see cref="SafeArchiveExtractor"/>: die Auflagen an ein Archiv aus
/// fremder Hand.
/// </summary>
/// <remarks>
/// Das Archiv kommt aus dem Netz. Was es enthält, bestimmt niemand aus diesem Haus —
/// deshalb wird hier gegen genau die Formen geprüft, gegen die die Auflagen schützen
/// sollen, statt gegen ein gutmütiges Beispielarchiv.
/// </remarks>
public sealed class SafeArchiveExtractorTests : IDisposable
{
    private readonly string _directory;
    private readonly string _zipPath;
    private readonly string _targetPath;

    public SafeArchiveExtractorTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "LookAwayArchive", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_directory);
        _zipPath = Path.Combine(_directory, "paket.zip");
        _targetPath = Path.Combine(_directory, "ziel");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
        catch (UnauthorizedAccessException)
        {
            // best effort
        }
    }

    [Fact]
    public void ExtractsFilesAndDirectories()
    {
        CreateArchive(archive =>
        {
            WriteEntry(archive, "LookAway.exe", "programm");
            WriteEntry(archive, "Themes/Light.xaml", "belegung");
        });

        SafeArchiveExtractor.ExtractTo(_zipPath, _targetPath);

        Assert.Equal("programm", File.ReadAllText(Path.Combine(_targetPath, "LookAway.exe")));
        Assert.Equal("belegung", File.ReadAllText(Path.Combine(_targetPath, "Themes", "Light.xaml")));
    }

    /// <remarks>
    /// Ein Eintrag mit <c>..</c> im Namen schriebe sonst neben das Zielverzeichnis —
    /// bei einem Update, das mit Schreibrechten auf das Programmverzeichnis läuft, wäre
    /// das der direkte Weg zu einer untergeschobenen Datei an beliebiger Stelle.
    /// </remarks>
    [Theory]
    [InlineData("../ausbruch.txt")]
    [InlineData("unterordner/../../ausbruch.txt")]
    public void RejectsEntriesThatLeaveTheTargetDirectory(string entryName)
    {
        CreateArchive(archive => WriteEntry(archive, entryName, "böse"));

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SafeArchiveExtractor.ExtractTo(_zipPath, _targetPath));

        Assert.Contains("Pfadverlassen", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_directory, "ausbruch.txt")));
    }

    /// <remarks>
    /// Die Markierung entscheidet, woher das Programm seine Einstellungen liest. Käme
    /// sie aus dem Paket in den Staging-Ordner, hielte sich der Helfer beim Einspielen
    /// für eine portable Installation, suchte den vermerkten Hash am falschen Ort und
    /// lehnte das eigene Update ab.
    /// </remarks>
    [Fact]
    public void SkipsThePortableMarker()
    {
        CreateArchive(archive =>
        {
            WriteEntry(archive, AppPaths.PortableFlagFileName, "markierung");
            WriteEntry(archive, "LookAway.exe", "programm");
        });

        SafeArchiveExtractor.ExtractTo(_zipPath, _targetPath);

        Assert.False(File.Exists(Path.Combine(_targetPath, AppPaths.PortableFlagFileName)));
        Assert.True(File.Exists(Path.Combine(_targetPath, "LookAway.exe")));
    }

    [Fact]
    public void CreatesTheTargetDirectoryIfItIsMissing()
    {
        CreateArchive(archive => WriteEntry(archive, "LookAway.exe", "programm"));

        SafeArchiveExtractor.ExtractTo(_zipPath, _targetPath);

        Assert.True(Directory.Exists(_targetPath));
    }

    [Fact]
    public void RejectsABlankPath()
    {
        _ = Assert.Throws<ArgumentException>(
            () => SafeArchiveExtractor.ExtractTo(" ", _targetPath));

        _ = Assert.Throws<ArgumentException>(
            () => SafeArchiveExtractor.ExtractTo(_zipPath, " "));
    }

    /// <remarks>
    /// Verzeichniseinträge tragen keinen Inhalt und enden auf einen Schrägstrich; sie
    /// müssen angelegt werden, damit ein Paket mit leerem Unterordner nicht scheitert.
    /// </remarks>
    [Fact]
    public void CreatesEmptyDirectoryEntries()
    {
        CreateArchive(archive => _ = archive.CreateEntry("leer/"));

        SafeArchiveExtractor.ExtractTo(_zipPath, _targetPath);

        Assert.True(Directory.Exists(Path.Combine(_targetPath, "leer")));
    }

    private void CreateArchive(Action<ZipArchive> fill)
    {
        using FileStream stream = File.Create(_zipPath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        fill(archive);
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using Stream target = entry.Open();
        target.Write(Encoding.UTF8.GetBytes(content));
    }
}
