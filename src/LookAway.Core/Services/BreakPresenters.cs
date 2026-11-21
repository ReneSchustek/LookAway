using LookAway.Core.Interfaces;

namespace LookAway.Core.Services;

/// <summary>
/// Die drei Anzeigen, über die der Pausen-Ablauf mit dem Benutzer spricht: Erinnerung, Overlay
/// während der Pause und das Symbol im Infobereich.
/// </summary>
/// <param name="Reminder">Zeigt die Erinnerung und meldet die Wahl des Benutzers.</param>
/// <param name="Overlay">Deckt während der Pause die Bildschirme ab.</param>
/// <param name="Tray">Zeigt Modell und „Nicht stören“ im Infobereich.</param>
public sealed record BreakPresenters(IReminderPresenter Reminder, IBreakOverlayPresenter Overlay, ITrayController Tray);
