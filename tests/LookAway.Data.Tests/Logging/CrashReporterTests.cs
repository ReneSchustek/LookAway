using LookAway.Data.Logging;

namespace LookAway.Data.Tests;

/// <summary>
/// Integrationstests für den <see cref="CrashReporter"/>.
/// </summary>
public sealed class CrashReporterTests : IDisposable
{
    private readonly string _crashDirectory;

    /// <summary>
    /// Legt für jeden Test ein eigenes Temp-Verzeichnis an.
    /// </summary>
    public CrashReporterTests()
    {
        _crashDirectory = Path.Combine(Path.GetTempPath(), "LookAway.Crashes.IT." + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Räumt das Temp-Verzeichnis nach dem Test wieder ab.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_crashDirectory))
            {
                Directory.Delete(_crashDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Report_CreatesJsonFileInDirectory()
    {
        CrashReporter reporter = new(_crashDirectory);
        InvalidOperationException ex = new("kapow");

        reporter.Report(ex, "TestSource");

        string[] files = Directory.GetFiles(_crashDirectory, "crash-*.json");
        _ = Assert.Single(files);
    }

    [Fact]
    public void Report_PersistsEssentialFields()
    {
        CrashReporter reporter = new(_crashDirectory);
        InvalidOperationException ex = new("kapow");

        reporter.Report(ex, "TestSource");

        string file = Directory.GetFiles(_crashDirectory, "crash-*.json").Single();
        string json = File.ReadAllText(file);

        Assert.Contains("\"source\": \"TestSource\"", json, StringComparison.Ordinal);
        Assert.Contains("\"exceptionType\":", json, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", json, StringComparison.Ordinal);
        Assert.Contains("\"managedThreadId\":", json, StringComparison.Ordinal);
        Assert.Contains("\"osVersion\":", json, StringComparison.Ordinal);
        Assert.Contains("\"runtimeVersion\":", json, StringComparison.Ordinal);
        Assert.Contains("\"timestamp\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_SanitizesUserNameInStackTrace()
    {
        LogMessageSanitizer sanitizer = new(
            @"C:\Users\testuser\AppData\Local",
            @"C:\Users\testuser\AppData\Roaming",
            @"C:\Users\testuser",
            "testuser");
        CrashReporter reporter = new(_crashDirectory, sanitizer);
        InvalidOperationException ex = new(@"User testuser im Pfad C:\Users\testuser\Documents");

        reporter.Report(ex, "TestSource");

        string json = File.ReadAllText(Directory.GetFiles(_crashDirectory, "crash-*.json").Single());
        Assert.DoesNotContain("testuser", json, StringComparison.Ordinal);
        Assert.Contains("<user>", json, StringComparison.Ordinal);
    }

    [Fact]
    public void HasUnresolvedCrashes_ReturnsFalseWhenNoCrashesExist()
    {
        CrashReporter reporter = new(_crashDirectory);

        Assert.False(reporter.HasUnresolvedCrashes());
    }

    [Fact]
    public void HasUnresolvedCrashes_ReturnsTrueAfterReport()
    {
        CrashReporter reporter = new(_crashDirectory);
        reporter.Report(new InvalidOperationException("x"), "TestSource");

        Assert.True(reporter.HasUnresolvedCrashes());
    }

    [Fact]
    public async Task MarkResolved_HidesExistingCrashes()
    {
        CrashReporter reporter = new(_crashDirectory);
        reporter.Report(new InvalidOperationException("x"), "TestSource");
        Assert.True(reporter.HasUnresolvedCrashes());

        // FileSystem-Timestamp-Auflösung: minimal warten, damit ack > crashtime gilt.
        await Task.Delay(50);
        reporter.MarkResolved();

        Assert.False(reporter.HasUnresolvedCrashes());
    }

    [Fact]
    public async Task NewCrashAfterMarkResolved_BecomesUnresolved()
    {
        CrashReporter reporter = new(_crashDirectory);
        reporter.Report(new InvalidOperationException("x"), "FirstSource");
        await Task.Delay(50);
        reporter.MarkResolved();
        Assert.False(reporter.HasUnresolvedCrashes());

        await Task.Delay(50);
        reporter.Report(new InvalidOperationException("y"), "SecondSource");

        Assert.True(reporter.HasUnresolvedCrashes());
    }

    [Fact]
    public void Report_RejectsNullException()
    {
        CrashReporter reporter = new(_crashDirectory);

        _ = Assert.Throws<ArgumentNullException>(
            () => reporter.Report(null!, "TestSource"));
    }

    [Fact]
    public void Report_RejectsBlankSource()
    {
        CrashReporter reporter = new(_crashDirectory);

        _ = Assert.Throws<ArgumentException>(
            () => reporter.Report(new InvalidOperationException("x"), "  "));
    }

    [Fact]
    public void Report_DoesNotCrashWhenDirectoryIsInvalid()
    {
        // Pfad mit ungültigen Zeichen — Directory.CreateDirectory wirft, was geschluckt werden muss.
        CrashReporter reporter = new(@"C:\This\Path\Does\Not\Exist\__InvalidChar__\\?invalid?");

        // Test-Erwartung: Methode wirft NICHT.
        reporter.Report(new InvalidOperationException("x"), "TestSource");
    }

    /// <remarks>
    /// Das Verzeichnis bleibt bestehen, nachdem der letzte Bericht von Hand gelöscht
    /// wurde. Ein leeres Verzeichnis ist kein offener Absturz.
    /// </remarks>
    [Fact]
    public void HasUnresolvedCrashes_WithEmptyDirectory_ReturnsFalse()
    {
        _ = Directory.CreateDirectory(_crashDirectory);
        CrashReporter reporter = new(_crashDirectory);

        Assert.False(reporter.HasUnresolvedCrashes());
    }

    /// <remarks>
    /// Beim Berichten wird der Bestätigungsvermerk gelöscht, sodass der Vergleich der
    /// Zeitstempel im eigenen Ablauf nie greift. Er greift, wenn der Absturz von
    /// woanders kommt — von einer zweiten Instanz oder aus einer gesicherten Ablage.
    /// Ohne diesen Vergleich bliebe ein solcher Absturz unbemerkt.
    /// </remarks>
    [Fact]
    public void HasUnresolvedCrashes_WithForeignCrashNewerThanMarker_ReturnsTrue()
    {
        _ = Directory.CreateDirectory(_crashDirectory);
        DateTime marker = DateTime.UtcNow.AddMinutes(-10);

        string ackPath = Path.Combine(_crashDirectory, ".acknowledged");
        File.WriteAllText(ackPath, "bestätigt");
        File.SetLastWriteTimeUtc(ackPath, marker);

        string crashPath = Path.Combine(_crashDirectory, "crash-20260811-120000-000.json");
        File.WriteAllText(crashPath, "{}");
        File.SetLastWriteTimeUtc(crashPath, marker.AddMinutes(5));

        Assert.True(new CrashReporter(_crashDirectory).HasUnresolvedCrashes());
    }

    /// <remarks>
    /// Umgekehrt: Ein Absturz, der älter ist als der Vermerk, wurde bereits gesehen.
    /// </remarks>
    [Fact]
    public void HasUnresolvedCrashes_WithForeignCrashOlderThanMarker_ReturnsFalse()
    {
        _ = Directory.CreateDirectory(_crashDirectory);
        DateTime marker = DateTime.UtcNow.AddMinutes(-10);

        string crashPath = Path.Combine(_crashDirectory, "crash-20260811-120000-000.json");
        File.WriteAllText(crashPath, "{}");
        File.SetLastWriteTimeUtc(crashPath, marker.AddMinutes(-5));

        string ackPath = Path.Combine(_crashDirectory, ".acknowledged");
        File.WriteAllText(ackPath, "bestätigt");
        File.SetLastWriteTimeUtc(ackPath, marker);

        Assert.False(new CrashReporter(_crashDirectory).HasUnresolvedCrashes());
    }

    /// <remarks>
    /// Ohne Verzeichnis gab es nie einen Absturz — es dafür anzulegen wäre verkehrt.
    /// </remarks>
    [Fact]
    public void MarkResolved_WithoutDirectory_CreatesNothing()
    {
        new CrashReporter(_crashDirectory).MarkResolved();

        Assert.False(Directory.Exists(_crashDirectory));
    }

    [Fact]
    public void MarkResolved_WithLockedMarker_IsIgnored()
    {
        _ = Directory.CreateDirectory(_crashDirectory);
        CrashReporter reporter = new(_crashDirectory);
        using LockedFile locked = new(Path.Combine(_crashDirectory, ".acknowledged"));

        Exception? thrown = Record.Exception(reporter.MarkResolved);

        Assert.Null(thrown);
    }
}
