using System.Globalization;
using LookAway.Data.Logging;
using Microsoft.Extensions.Logging;

namespace LookAway.Tests.Integration.Data.Logging;

/// <summary>
/// Integrationstests für den <see cref="RollingFileSink"/>: prüfen
/// Datei-Layout, Rotation und Retention auf einem echten Temp-Verzeichnis.
/// </summary>
public sealed class RollingFileSinkTests : IDisposable
{
    private readonly string _logDirectory;

    /// <summary>
    /// Legt für jeden Test ein eigenes Temp-Verzeichnis an.
    /// </summary>
    public RollingFileSinkTests()
    {
        _logDirectory = Path.Combine(Path.GetTempPath(), "LookAway.Logs.IT." + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_logDirectory);
    }

    /// <summary>
    /// Räumt das Temp-Verzeichnis nach dem Test wieder ab.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_logDirectory))
            {
                Directory.Delete(_logDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Write_CreatesFileForCurrentDay()
    {
        using RollingFileSink sink = CreateSink();
        DateTimeOffset timestamp = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

        sink.Write(timestamp, LogLevel.Information, "TestCategory", "Hallo Welt", null);

        string expected = Path.Combine(_logDirectory, "lookaway-2026-05-10.log");
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public void Write_FormatsLineWithTimestampLevelCategoryAndMessage()
    {
        using RollingFileSink sink = CreateSink();
        DateTimeOffset timestamp = new(2026, 5, 10, 12, 30, 45, 123, TimeSpan.Zero);

        sink.Write(timestamp, LogLevel.Warning, "LookAway.Foo", "Pause übersprungen", null);

        string content = ReadAllLogs();
        Assert.Contains("[2026-05-10T12:30:45.123Z]", content, StringComparison.Ordinal);
        Assert.Contains("[Warning]", content, StringComparison.Ordinal);
        Assert.Contains("LookAway.Foo: Pause übersprungen", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_AppendsExceptionStackTrace()
    {
        using RollingFileSink sink = CreateSink();
        InvalidOperationException ex = new("kapow");

        sink.Write(DateTimeOffset.UtcNow, LogLevel.Error, "Cat", "Was fehlerhaft", ex);

        string content = ReadAllLogs();
        Assert.Contains("InvalidOperationException", content, StringComparison.Ordinal);
        Assert.Contains("kapow", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_DifferentDaysProduceDifferentFiles()
    {
        using RollingFileSink sink = CreateSink();
        DateTimeOffset day1 = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset day2 = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

        sink.Write(day1, LogLevel.Information, "Cat", "Tag1", null);
        sink.Write(day2, LogLevel.Information, "Cat", "Tag2", null);

        Assert.True(File.Exists(Path.Combine(_logDirectory, "lookaway-2026-05-10.log")));
        Assert.True(File.Exists(Path.Combine(_logDirectory, "lookaway-2026-05-11.log")));
    }

    [Fact]
    public void Write_SanitizesUserNameInMessage()
    {
        LogMessageSanitizer sanitizer = new(
            @"C:\Users\testuser\AppData\Local",
            @"C:\Users\testuser\AppData\Roaming",
            @"C:\Users\testuser",
            "testuser");
        using RollingFileSink sink = new(_logDirectory, retentionDays: 7, sanitizer);

        sink.Write(
            DateTimeOffset.UtcNow,
            LogLevel.Information,
            "Cat",
            @"User testuser hat C:\Users\testuser\AppData\Roaming\LookAway geöffnet",
            null);

        string content = ReadAllLogs();
        Assert.DoesNotContain("testuser", content, StringComparison.Ordinal);
        Assert.Contains("<user>", content, StringComparison.Ordinal);
        Assert.Contains("%APPDATA%", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PruneNow_DeletesFilesOlderThanRetention()
    {
        using RollingFileSink sink = new(_logDirectory, retentionDays: 7);
        DateTime today = DateTime.UtcNow.Date;

        string oldFile = WriteLogFileWithDate(today.AddDays(-10), "old content");
        string keepFile = WriteLogFileWithDate(today.AddDays(-2), "fresh content");
        string todayFile = WriteLogFileWithDate(today, "today");

        sink.PruneNow();

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(keepFile));
        Assert.True(File.Exists(todayFile));
    }

    [Fact]
    public void PruneNow_IgnoresFilesNotMatchingPattern()
    {
        using RollingFileSink sink = new(_logDirectory, retentionDays: 7);
        string foreignFile = Path.Combine(_logDirectory, "fremde-datei.txt");
        File.WriteAllText(foreignFile, "kein log");
        File.SetLastWriteTimeUtc(foreignFile, DateTime.UtcNow.AddDays(-30));

        sink.PruneNow();

        Assert.True(File.Exists(foreignFile));
    }

    [Fact]
    public void Write_ToleratesDisposalAndSilentlyDoesNothing()
    {
        RollingFileSink sink = CreateSink();
        sink.Dispose();

        // Darf keine Exception werfen.
        sink.Write(DateTimeOffset.UtcNow, LogLevel.Error, "Cat", "After dispose", null);

        Assert.False(Directory.EnumerateFiles(_logDirectory, "lookaway-*.log").Any());
    }

    [Fact]
    public async Task Write_ConcurrentCallsDoNotCorruptFile()
    {
        using RollingFileSink sink = CreateSink();
        IEnumerable<Task> writers = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => sink.Write(
                DateTimeOffset.UtcNow,
                LogLevel.Information,
                "Cat",
                "msg-" + i.ToString(CultureInfo.InvariantCulture),
                null)));

        await Task.WhenAll(writers);

        string content = ReadAllLogs();
        for (int i = 0; i < 50; i++)
        {
            Assert.Contains(
                "msg-" + i.ToString(CultureInfo.InvariantCulture),
                content,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Constructor_RejectsBlankDirectory()
    {
        _ = Assert.Throws<ArgumentException>(() => new RollingFileSink(string.Empty));
    }

    [Fact]
    public void Constructor_RejectsRetentionLessThanOne()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new RollingFileSink(_logDirectory, retentionDays: 0));
    }

    private RollingFileSink CreateSink() => new(_logDirectory, retentionDays: 7);

    private string ReadAllLogs()
    {
        string output = string.Empty;
        foreach (string file in Directory.EnumerateFiles(_logDirectory, "lookaway-*.log"))
        {
            output += File.ReadAllText(file);
        }
        return output;
    }

    private string WriteLogFileWithDate(DateTime date, string content)
    {
        string fileName = $"lookaway-{date:yyyy-MM-dd}.log";
        string path = Path.Combine(_logDirectory, fileName);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, date);
        return path;
    }
}
