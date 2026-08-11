using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// In-Memory-Fake für <see cref="ILogEntryReader"/>. Liefert eine feste Liste und
/// zählt die Lesevorgänge.
/// </summary>
internal sealed class FakeLogEntryReader : ILogEntryReader
{
    private readonly IReadOnlyList<LogEntry> _entries;

    /// <summary>Erzeugt den Fake, optional mit Einträgen.</summary>
    /// <param name="entries">Zu liefernde Einträge, neueste zuerst.</param>
    public FakeLogEntryReader(IReadOnlyList<LogEntry>? entries = null)
        => _entries = entries ?? [];

    /// <summary>Anzahl der <see cref="ReadRecentAsync"/>-Aufrufe.</summary>
    public int ReadCallCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<LogEntry>> ReadRecentAsync(
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        ReadCallCount++;
        return Task.FromResult<IReadOnlyList<LogEntry>>([.. _entries.Take(maxEntries)]);
    }
}
