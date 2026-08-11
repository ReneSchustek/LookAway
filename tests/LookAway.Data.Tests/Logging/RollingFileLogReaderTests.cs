using LookAway.Core.ValueObjects;
using LookAway.Data.Logging;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Tests.Logging;

/// <summary>
/// Tests für <see cref="RollingFileLogReader"/>: Reihenfolge über Dateigrenzen,
/// Obergrenze und das Verhalten, wenn noch nichts protokolliert wurde.
/// </summary>
public sealed class RollingFileLogReaderTests : IDisposable
{
    private readonly string _directory;

    /// <summary>Legt ein leeres Protokollverzeichnis für den Testlauf an.</summary>
    public RollingFileLogReaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "lookaway-logreader-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task ReadRecentAsync_ReturnsNewestEntryFirst()
    {
        WriteLog("2026-08-10", "[2026-08-10T08:00:00.000Z] [Information] A: gestern");
        WriteLog("2026-08-11", "[2026-08-11T08:00:00.000Z] [Information] A: heute früh");
        AppendLog("2026-08-11", "[2026-08-11T12:00:00.000Z] [Warning] A: heute mittag");

        IReadOnlyList<LogEntry> entries = await new RollingFileLogReader(_directory).ReadRecentAsync(10);

        Assert.Equal(3, entries.Count);
        Assert.Equal("heute mittag", entries[0].Message);
        Assert.Equal("heute früh", entries[1].Message);
        Assert.Equal("gestern", entries[2].Message);
    }

    [Fact]
    public async Task ReadRecentAsync_StopsAtTheRequestedCount()
    {
        WriteLog("2026-08-11", "[2026-08-11T08:00:00.000Z] [Information] A: eins");
        AppendLog("2026-08-11", "[2026-08-11T09:00:00.000Z] [Information] A: zwei");
        AppendLog("2026-08-11", "[2026-08-11T10:00:00.000Z] [Information] A: drei");

        IReadOnlyList<LogEntry> entries = await new RollingFileLogReader(_directory).ReadRecentAsync(2);

        Assert.Equal(2, entries.Count);
        Assert.Equal("drei", entries[0].Message);
        Assert.Equal("zwei", entries[1].Message);
    }

    [Fact]
    public async Task ReadRecentAsync_KeepsTheExceptionStackWithItsEntry()
    {
        WriteLog(
            "2026-08-11",
            "[2026-08-11T08:00:00.000Z] [Error] A: Fehler beim Speichern",
            "System.IO.IOException: Zugriff verweigert");

        LogEntry entry = Assert.Single(await new RollingFileLogReader(_directory).ReadRecentAsync(10));

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("IOException", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadRecentAsync_ReturnsEmptyWhenNothingWasLogged()
        => Assert.Empty(await new RollingFileLogReader(_directory).ReadRecentAsync(10));

    /// <remarks>
    /// Vor dem ersten Start gibt es das Verzeichnis noch nicht. Das ist kein Fehler,
    /// den der Benutzer beheben könnte — die Ansicht zeigt dann ihren Leerzustand.
    /// </remarks>
    [Fact]
    public async Task ReadRecentAsync_ReturnsEmptyWhenTheDirectoryIsMissing()
    {
        string missing = Path.Combine(_directory, "gibt-es-nicht");

        Assert.Empty(await new RollingFileLogReader(missing).ReadRecentAsync(10));
    }

    [Fact]
    public async Task ReadRecentAsync_IgnoresForeignFiles()
    {
        WriteLog("2026-08-11", "[2026-08-11T08:00:00.000Z] [Information] A: gehört dazu");
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "notizen.txt"),
            "[2026-08-11T09:00:00.000Z] [Information] A: gehört nicht dazu");

        LogEntry entry = Assert.Single(await new RollingFileLogReader(_directory).ReadRecentAsync(10));

        Assert.Equal("gehört dazu", entry.Message);
    }

    [Fact]
    public void Constructor_RejectsAnEmptyDirectory()
        => Assert.Throws<ArgumentException>(() => new RollingFileLogReader("  "));

    [Fact]
    public async Task ReadRecentAsync_RejectsANonPositiveCount()
        => await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => new RollingFileLogReader(_directory).ReadRecentAsync(0));

    /// <summary>Räumt das Testverzeichnis wieder ab.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist bestes Bemühen; ein belegter Ordner darf den Lauf nicht kippen.
        }
    }

    private void WriteLog(string date, params string[] lines)
        => File.WriteAllLines(Path.Combine(_directory, $"lookaway-{date}.log"), lines);

    private void AppendLog(string date, params string[] lines)
        => File.AppendAllLines(Path.Combine(_directory, $"lookaway-{date}.log"), lines);
}
