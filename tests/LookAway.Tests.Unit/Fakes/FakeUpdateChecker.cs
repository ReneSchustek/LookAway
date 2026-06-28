using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// Test-Fake fuer <see cref="IUpdateChecker"/>: liefert ein vorgegebenes
/// Ergebnis und zaehlt die Aufrufe.
/// </summary>
internal sealed class FakeUpdateChecker : IUpdateChecker
{
    private readonly UpdateInfo _result;

    /// <summary>Erzeugt den Fake mit einem festen Ergebnis.</summary>
    /// <param name="result">Zurueckzugebendes Ergebnis; <c>null</c> = kein Update.</param>
    public FakeUpdateChecker(UpdateInfo? result = null)
    {
        _result = result ?? UpdateInfo.NoUpdate(new Version(1, 0, 0));
    }

    /// <summary>Anzahl der Pruef-Aufrufe.</summary>
    public int CheckCallCount { get; private set; }

    /// <inheritdoc />
    public Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        CheckCallCount++;
        return Task.FromResult(_result);
    }
}
