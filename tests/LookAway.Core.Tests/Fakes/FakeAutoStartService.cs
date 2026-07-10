using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;

namespace LookAway.Core.Tests.Fakes;

/// <summary>
/// Test-Fake für <see cref="IAutoStartService"/>. Hält den An/Aus-Zustand im
/// Speicher und zählt die Aufrufe, damit die "Settings-Logik" des
/// <c>AutoStartCoordinator</c> ohne echte Registry geprüft werden kann.
/// </summary>
internal sealed class FakeAutoStartService : IAutoStartService
{
    private bool _enabled;

    /// <summary>Erzeugt den Fake mit einem Anfangszustand.</summary>
    /// <param name="initiallyEnabled">Startzustand des Autostart-Eintrags.</param>
    public FakeAutoStartService(bool initiallyEnabled = false)
    {
        _enabled = initiallyEnabled;
    }

    /// <summary>Anzahl der <see cref="Enable"/>-Aufrufe.</summary>
    public int EnableCallCount { get; private set; }

    /// <summary>Anzahl der <see cref="Disable"/>-Aufrufe.</summary>
    public int DisableCallCount { get; private set; }

    /// <summary>
    /// Wenn gesetzt, wirft <see cref="Enable"/> eine
    /// <see cref="AutoStartException"/> — zum Prüfen der Fehlerpfade.
    /// </summary>
    public bool ThrowOnEnable { get; set; }

    /// <inheritdoc />
    public bool IsEnabled() => _enabled;

    /// <inheritdoc />
    public void Enable()
    {
        if (ThrowOnEnable)
        {
            throw new AutoStartException("Test: Enable fehlgeschlagen.");
        }

        EnableCallCount++;
        _enabled = true;
    }

    /// <inheritdoc />
    public void Disable()
    {
        DisableCallCount++;
        _enabled = false;
    }
}
