using LookAway.Core.Domain;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Was die Hintergrund-Schleifen der App an den Pausen-Ablauf melden: Ereignisse des Timers, eine
/// nachzuholende Erinnerung und den Stand der Vollbild-Unterdrückung.
/// </summary>
/// <remarks>
/// Umgesetzt vom <see cref="Services.BreakCoordinator"/>. Die Schleifen kennen nur diese drei
/// Meldungen und lassen sich damit ohne den ganzen Pausen-Ablauf prüfen.
/// </remarks>
public interface IBreakSignalSink
{
    /// <summary>Verarbeitet ein Ereignis des Timers.</summary>
    /// <param name="timerEvent">Das Ereignis.</param>
    void HandleTimerEvent(TimerEvent timerEvent);

    /// <summary>Zeigt die Erinnerung, die während der Unterdrückung ausgefallen ist.</summary>
    void RequestReminder();

    /// <summary>Übernimmt, ob eine Vollbild-Anwendung Erinnerungen gerade unterdrückt.</summary>
    /// <param name="fullscreenDndActive">Unterdrückt eine Vollbild-Anwendung gerade?</param>
    void UpdateDndIndicator(bool fullscreenDndActive);
}
