using Microsoft.Extensions.Logging;

namespace LookAway.Data.Logging;

/// <summary>
/// <see cref="ILogger"/>-Implementation, die Eintraege in ein
/// <see cref="RollingFileSink"/> weiterreicht.
/// </summary>
internal sealed class RollingFileLogger : ILogger
{
    private readonly string _category;
    private readonly RollingFileSink _sink;
    private readonly Func<LogLevel> _minimumLevelAccessor;

    public RollingFileLogger(string category, RollingFileSink sink, Func<LogLevel> minimumLevelAccessor)
    {
        _category = category;
        _sink = sink;
        _minimumLevelAccessor = minimumLevelAccessor;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevelAccessor();

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        try
        {
            string message = formatter(state, exception);
            _sink.Write(DateTimeOffset.UtcNow, logLevel, _category, message, exception);
        }
        catch (IOException)
        {
            // Schreibfehler nicht hochwerfen — siehe BRIEF013.
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (InvalidOperationException)
        {
            // Formatter-Fehler tolerieren statt App-Crash zu provozieren.
        }
    }
}
