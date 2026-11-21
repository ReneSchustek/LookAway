using LookAway.App.ViewModels;

namespace LookAway.App.Services;

/// <summary>
/// Die Fensterseite des Pausen-Overlays: auf den Fenster-Thread wechseln, die Overlay-Fenster
/// öffnen, den Sekundentakt laufen lassen, die Fenster auffrischen und schließen.
/// </summary>
/// <remarks>
/// Der <see cref="BreakOverlayPresenter"/> entscheidet, wann was geschieht; diese Schnittstelle
/// führt es aus. So lässt sich der Ablauf einer Pause ohne Fenster prüfen.
/// </remarks>
internal interface IBreakOverlayUi
{
    /// <summary>Führt <paramref name="action"/> auf dem Fenster-Thread aus.</summary>
    /// <param name="action">Auszuführende Aktion.</param>
    void Post(Action action);

    /// <summary>
    /// Öffnet ein Overlay je Monitor (oder nur auf dem Hauptmonitor). Den Inhalt zeigt nur der
    /// Hauptmonitor, die übrigen werden nur abgedunkelt.
    /// </summary>
    /// <param name="viewModel">Zustand der Pause, den alle Fenster teilen.</param>
    /// <param name="look">Farbe und gegebenenfalls Bild, die jedes Fenster zeichnet.</param>
    /// <param name="allScreens">Alle Monitore abdunkeln statt nur den Hauptmonitor.</param>
    void OpenWindows(BreakOverlayViewModel viewModel, OverlayLook look, bool allScreens);

    /// <summary>Startet den Sekundentakt der Pause.</summary>
    /// <param name="tick">Wird jede Sekunde auf dem Fenster-Thread aufgerufen.</param>
    void StartCountdown(Action tick);

    /// <summary>Zeigt den neuen Countdown und holt die Fenster wieder nach vorn.</summary>
    void RefreshWindows();

    /// <summary>Hält den Takt an und schließt alle Overlay-Fenster.</summary>
    void CloseWindows();
}
