using Microsoft.Extensions.Logging;

namespace LookAway.Data.Tests;

/// <summary>
/// Logger, der jeden Eintrag mit Stufe und Ereignisnummer festhält — für Tests, die belegen
/// müssen, dass ein Fehlschlag im Protokoll landet.
/// </summary>
/// <typeparam name="T">Kategorie des Loggers.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, int EventId)> _entries = [];

    /// <summary>Die bisher geschriebenen Einträge.</summary>
    public IReadOnlyList<(LogLevel Level, int EventId)> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _entries.Add((logLevel, eventId.Id));
}
