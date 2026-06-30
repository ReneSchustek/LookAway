using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Logging;

/// <summary>
/// Eigener <see cref="ILoggerProvider"/>, der pro Kategorie einen
/// <see cref="RollingFileLogger"/> erzeugt und an einen geteilten
/// <see cref="RollingFileSink"/> weiterreicht.
/// </summary>
/// <remarks>
/// Der Provider hält den Sink als <see cref="IDisposable"/>; der Aufrufer
/// (typischerweise der DI-Container) ist für das Disposing zuständig.
/// </remarks>
[ProviderAlias("LookAwayFile")]
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly RollingFileSink _sink;
    private readonly ConcurrentDictionary<string, RollingFileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly bool _ownsSink;
    private LogLevel _minimumLevel;
    private bool _disposed;

    /// <summary>
    /// Erzeugt einen Provider, der einen eigenen Sink mit Default-Retention
    /// in das angegebene Verzeichnis anlegt.
    /// </summary>
    /// <param name="logDirectory">Zielverzeichnis für die Log-Dateien.</param>
    /// <param name="minimumLevel">Minimaler Log-Level (Default: Information).</param>
    public RollingFileLoggerProvider(string logDirectory, LogLevel minimumLevel = LogLevel.Information)
        : this(new RollingFileSink(logDirectory), minimumLevel, ownsSink: true)
    {
    }

    /// <summary>
    /// Erzeugt einen Provider mit einem extern verwalteten Sink. Vorgesehen
    /// für Tests, die den Sink selbst kontrollieren wollen.
    /// </summary>
    /// <param name="sink">Der zu verwendende Sink.</param>
    /// <param name="minimumLevel">Minimaler Log-Level.</param>
    /// <param name="ownsSink">Wenn <c>true</c>, wird der Sink mit dem Provider disposed.</param>
    public RollingFileLoggerProvider(RollingFileSink sink, LogLevel minimumLevel, bool ownsSink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        _sink = sink;
        _minimumLevel = minimumLevel;
        _ownsSink = ownsSink;
    }

    /// <summary>
    /// Setzt den minimalen Log-Level zur Laufzeit. Wirkt sofort für alle
    /// bereits erstellten und zukünftigen Logger.
    /// </summary>
    /// <param name="level">Neuer Mindest-Level.</param>
    public void SetMinimumLevel(LogLevel level) => _minimumLevel = level;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _loggers.GetOrAdd(categoryName, name => new RollingFileLogger(name, _sink, () => _minimumLevel));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _loggers.Clear();
        if (_ownsSink)
        {
            _sink.Dispose();
        }
    }
}
