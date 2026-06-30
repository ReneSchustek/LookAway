using LookAway.Core.Enums;

namespace LookAway.Application.Coordination;

/// <summary>
/// Zeigt das Pause-Erinnerungsfenster. UI-freie Abstraktion, damit der
/// <see cref="BreakCoordinator"/> ohne WinUI-Abhaengigkeit testbar bleibt.
/// </summary>
public interface IReminderPresenter
{
    /// <summary>Ist gerade eine Erinnerung sichtbar?</summary>
    bool IsReminderOpen { get; }

    /// <summary>Zeigt eine Erinnerung; bei bereits offener passiert nichts.</summary>
    /// <param name="model">Aktives Pausenmodell (fuer den Hinweistext).</param>
    /// <param name="onResult">Callback mit der gewaehlten Aktion.</param>
    void Show(BreakModel model, Action<ReminderResult> onResult);
}

/// <summary>
/// Zeigt das abdunkelnde Pausen-Overlay (eines je Monitor). UI-freie Abstraktion.
/// </summary>
public interface IBreakOverlayPresenter
{
    /// <summary>Ist gerade ein Overlay sichtbar?</summary>
    bool IsOverlayOpen { get; }

    /// <summary>Zeigt das Overlay fuer die Pausendauer; bei offenem passiert nichts.</summary>
    /// <param name="model">Aktives Pausenmodell.</param>
    /// <param name="breakDuration">Dauer der Pause.</param>
    /// <param name="overlayColorHex">Hintergrundfarbe als <c>#AARRGGBB</c>/<c>#RRGGBB</c>.</param>
    /// <param name="darkenAllScreens">Alle Monitore abdecken?</param>
    /// <param name="onEnded">Callback mit dem Grund des Pausenendes.</param>
    void Show(BreakModel model, TimeSpan breakDuration, string overlayColorHex, bool darkenAllScreens, Action<BreakEndReason> onEnded);

    /// <summary>Schliesst offene Overlays ohne den Ende-Callback auszuloesen.</summary>
    void Close();
}

/// <summary>
/// Schmale Sicht auf das Tray-Icon, soweit der Coordinator es steuert
/// (aktives Modell und Nicht-stoeren-Anzeige).
/// </summary>
public interface ITrayController
{
    /// <summary>Setzt das angezeigte aktive Pausenmodell.</summary>
    /// <param name="model">Aktives Modell.</param>
    void SetActiveModel(BreakModel model);

    /// <summary>Schaltet die Nicht-stoeren-Anzeige um.</summary>
    /// <param name="isDndActive">Ist DND aktiv?</param>
    void SetDndActive(bool isDndActive);
}
