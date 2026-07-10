using LookAway.Data.Logging;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Tests;

/// <summary>
/// Integrationstests für den <see cref="RollingFileLoggerProvider"/>:
/// stellt sicher, dass Logger pro Kategorie wiederverwendet werden und
/// IO-/Formatter-Fehler die Anwendung nicht zum Absturz bringen.
/// </summary>
public sealed class RollingFileLoggerProviderTests : IDisposable
{
    private readonly string _logDirectory;

    /// <summary>
    /// Legt für jeden Test ein eigenes Temp-Verzeichnis an.
    /// </summary>
    public RollingFileLoggerProviderTests()
    {
        _logDirectory = Path.Combine(Path.GetTempPath(), "LookAway.LogProvider.IT." + Guid.NewGuid().ToString("N"));
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
    public void CreateLogger_ReturnsSameInstanceForSameCategory()
    {
        using RollingFileLoggerProvider provider = new(_logDirectory);

        ILogger first = provider.CreateLogger("Foo");
        ILogger second = provider.CreateLogger("Foo");

        Assert.Same(first, second);
    }

    [Fact]
    public void CreateLogger_AfterDispose_Throws()
    {
        RollingFileLoggerProvider provider = new(_logDirectory);
        provider.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => provider.CreateLogger("Foo"));
    }

    [Fact]
    public void Logger_RespectsMinimumLevel()
    {
        using RollingFileLoggerProvider provider = new(_logDirectory, LogLevel.Warning);
        ILogger logger = provider.CreateLogger("TestCat");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void SetMinimumLevel_TakesEffectImmediately()
    {
        using RollingFileLoggerProvider provider = new(_logDirectory, LogLevel.Information);
        ILogger logger = provider.CreateLogger("TestCat");
        Assert.True(logger.IsEnabled(LogLevel.Information));

        provider.SetMinimumLevel(LogLevel.Error);

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void Logger_LogInformation_WritesToFile()
    {
        using RollingFileLoggerProvider provider = new(_logDirectory, LogLevel.Debug);
        ILogger logger = provider.CreateLogger("TestCat");

        logger.Log(
            LogLevel.Information,
            new EventId(0),
            "Hallo Welt",
            null,
            (state, _) => state);

        string content = string.Join('\n', Directory.EnumerateFiles(_logDirectory, "lookaway-*.log").Select(File.ReadAllText));
        Assert.Contains("Hallo Welt", content, StringComparison.Ordinal);
        Assert.Contains("TestCat", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Logger_FormatterException_DoesNotPropagate()
    {
        using RollingFileLoggerProvider provider = new(_logDirectory, LogLevel.Debug);
        ILogger logger = provider.CreateLogger("TestCat");

        // Logger.Log mit Formatter, der wirft — darf nicht hochwerfen.
        logger.Log(
            LogLevel.Information,
            new EventId(0),
            "state",
            null,
            (_, _) => throw new InvalidOperationException("formatter kapow"));

        // Erfolg = keine Exception.
    }
}
