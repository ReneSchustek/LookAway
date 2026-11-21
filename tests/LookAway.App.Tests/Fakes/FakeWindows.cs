using LookAway.App.Services;

namespace LookAway.App.Tests.Fakes;

/// <summary>Führt Gepostetes sofort auf dem Test-Thread aus.</summary>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public int PostCount { get; private set; }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        PostCount++;
        action();
    }
}

/// <summary>Fenster ohne Oberfläche: zählt das Nach-vorn-Holen und lässt sich schließen.</summary>
internal sealed class FakeAppWindow : IAppWindow
{
    public event EventHandler? Closed;

    public int ActivateCount { get; private set; }

    public void Activate() => ActivateCount++;

    /// <summary>Schließt das Fenster, wie es der Benutzer täte.</summary>
    public void Close() => Closed?.Invoke(this, EventArgs.Empty);
}
