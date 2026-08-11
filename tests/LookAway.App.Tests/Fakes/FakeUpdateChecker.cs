using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Test-Fake für <see cref="IUpdateChecker"/>: liefert ein vorgegebenes
/// Ergebnis und zählt die Aufrufe.
/// </summary>
internal sealed class FakeUpdateChecker : IUpdateChecker
{
    private readonly UpdateInfo _result;

    /// <summary>Erzeugt den Fake mit einem festen Ergebnis.</summary>
    /// <param name="result">Zurückzugebendes Ergebnis; <c>null</c> = kein Update.</param>
    public FakeUpdateChecker(UpdateInfo? result = null)
    {
        _result = result ?? UpdateInfo.NoUpdate(new Version(1, 0, 0));
    }

    /// <summary>Anzahl der Prüf-Aufrufe.</summary>
    public int CheckCallCount { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Der Abbruch wird beachtet wie beim echten Dienst: Eine Attrappe, die ihn
    /// stillschweigend übergeht, ließe jeden Test grün werden, der das Gegenteil
    /// belegen soll.
    /// </remarks>
    public Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CheckCallCount++;
        return Task.FromResult(_result);
    }
}
