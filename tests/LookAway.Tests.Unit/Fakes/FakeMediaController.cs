using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>Test-Fake für <see cref="IMediaController"/>: zählt die Aufrufe.</summary>
internal sealed class FakeMediaController : IMediaController
{
    /// <summary>Anzahl der <see cref="PauseAllAsync"/>-Aufrufe.</summary>
    public int PauseCallCount { get; private set; }

    /// <summary>Anzahl der <see cref="ResumeAllAsync"/>-Aufrufe.</summary>
    public int ResumeCallCount { get; private set; }

    /// <inheritdoc />
    public Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        PauseCallCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        ResumeCallCount++;
        return Task.CompletedTask;
    }
}
