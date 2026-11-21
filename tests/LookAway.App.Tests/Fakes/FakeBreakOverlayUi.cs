using LookAway.App.Services;
using LookAway.App.ViewModels;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Fensterseite ohne Fenster: führt Gepostetes sofort aus, merkt sich den Sekundentakt zum
/// Auslösen von Hand und zählt Öffnen, Auffrischen und Schließen.
/// </summary>
internal sealed class FakeBreakOverlayUi : IBreakOverlayUi
{
    private Action? _tick;

    public int OpenCount { get; private set; }

    public int RefreshCount { get; private set; }

    public int CloseCount { get; private set; }

    public BreakOverlayViewModel? ViewModel { get; private set; }

    public OverlayLook? Look { get; private set; }

    public bool AllScreens { get; private set; }

    /// <summary>Wird beim Öffnen geworfen, wenn gesetzt (etwa ein Fehler des Vollbild-Fensters).</summary>
    public Exception? OpenFailure { get; set; }

    public void Post(Action action) => action();

    public void OpenWindows(BreakOverlayViewModel viewModel, OverlayLook look, bool allScreens)
    {
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        OpenCount++;
        ViewModel = viewModel;
        Look = look;
        AllScreens = allScreens;
    }

    public void StartCountdown(Action tick) => _tick = tick;

    public void RefreshWindows() => RefreshCount++;

    public void CloseWindows() => CloseCount++;

    /// <summary>Lässt eine Sekunde vergehen.</summary>
    public void Tick() => _tick?.Invoke();
}
