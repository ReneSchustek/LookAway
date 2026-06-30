using Microsoft.Extensions.Logging;

namespace LookAway.Data.Logging;

/// <summary>
/// <see cref="ILogger"/>-Implementation, die Einträge in ein
/// <see cref="RollingFileSink"/> weiterreicht.
/// </summary>
internal sealed class RollingFileLogger : ILogger
{
    private readonly string _category;
    private readonly RollingFileSink _sink;
    private readonly Func<LogLevel> _minimumLevelAccessor;

    /// <summary>Erzeugt einen Logger für eine Kategorie.</summary>
    /// <param name="category">Logger-Kategorie (i. d. R. der Typname).</param>
    /// <param name="sink">Ziel-Sink, der die Einträge schreibt.</param>
    /// <param name="minimumLevelAccessor">Liefert den aktuell wirksamen Mindest-Level (zur Laufzeit auswertbar).</param>
    public RollingFileLogger(string category, RollingFileSink sink, Func<LogLevel> minimumLevelAccessor)
    {
        _category = category;
        _sink = sink;
        _minimumLevelAccessor = minimumLevelAccessor;
    }

    /// <inheritdoc />
    /// <remarks>Scopes werden nicht unterstützt (der Datei-Sink kennt keine Verschachtelung), daher <see langword="null"/>.</remarks>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevelAccessor();

    /// <inheritdoc />
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
            // Schreibfehler nicht hochwerfen — der Logger darf die App nie zum Absturz bringen.
        }
        catch (UnauthorizedAccessException)
        {
            // wie oben: Schreibfehler bewusst geschluckt (Logger darf nie abstürzen).
        }
        catch (InvalidOperationException)
        {
            // Formatter-Fehler tolerieren statt App-Crash zu provozieren.
        }
    }
}
