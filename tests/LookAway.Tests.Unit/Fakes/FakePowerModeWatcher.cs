using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// Test-Fake für <see cref="IPowerModeWatcher"/>. <see cref="RaiseSuspending"/>
/// und <see cref="RaiseResuming"/> erlauben deterministisches Auslösen
/// der Plattform-Events.
/// </summary>
internal sealed class FakePowerModeWatcher : IPowerModeWatcher
{
    public event EventHandler? Suspending;

    public event EventHandler? Resuming;

    public bool Started { get; private set; }

    public bool Disposed { get; private set; }

    public void Start() => Started = true;

    public void RaiseSuspending() => Suspending?.Invoke(this, EventArgs.Empty);

    public void RaiseResuming() => Resuming?.Invoke(this, EventArgs.Empty);

    public void Dispose() => Disposed = true;
}
