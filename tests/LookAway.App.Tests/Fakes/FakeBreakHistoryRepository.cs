using LookAway.Core.Entities;
using LookAway.Core.Interfaces;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// In-Memory-Fake für <see cref="IBreakHistoryRepository"/>. Hält die
/// Sitzungen in einer Liste und ermöglicht deterministische Statistik-Tests.
/// </summary>
internal sealed class FakeBreakHistoryRepository : IBreakHistoryRepository
{
    private readonly List<BreakSession> _sessions;

    /// <summary>Erzeugt das Fake-Repository, optional mit Startdaten.</summary>
    /// <param name="initial">Anfangs vorhandene Sitzungen.</param>
    public FakeBreakHistoryRepository(IEnumerable<BreakSession>? initial = null)
    {
        _sessions = initial is null ? new List<BreakSession>() : new List<BreakSession>(initial);
    }

    /// <inheritdoc />
    public Task AppendAsync(BreakSession session, CancellationToken cancellationToken = default)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BreakSession>> LoadAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BreakSession>>(_sessions.ToList());

    /// <inheritdoc />
    public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        int removed = _sessions.RemoveAll(session => session.StartedAt < cutoff);
        return Task.FromResult(removed);
    }
}
