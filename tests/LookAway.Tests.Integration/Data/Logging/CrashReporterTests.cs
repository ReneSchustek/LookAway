using LookAway.Data.Logging;

namespace LookAway.Tests.Integration.Data.Logging;

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
}
