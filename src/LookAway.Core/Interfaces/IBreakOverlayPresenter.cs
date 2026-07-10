using LookAway.Core.Enums;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Zeigt das abdunkelnde Pausen-Overlay (eines je Monitor). UI-freie Abstraktion.
/// </summary>
public interface IBreakOverlayPresenter
{
    /// <summary>Ist gerade ein Overlay sichtbar?</summary>
    bool IsOverlayOpen { get; }

    /// <summary>Zeigt das Overlay für die Pausendauer; bei offenem passiert nichts.</summary>
    /// <param name="model">Aktives Pausenmodell.</param>
    /// <param name="breakDuration">Dauer der Pause.</param>
    /// <param name="overlayColorHex">Hintergrundfarbe als <c>#AARRGGBB</c>/<c>#RRGGBB</c>.</param>
    /// <param name="darkenAllScreens">Alle Monitore abdecken?</param>
    /// <param name="onEnded">Callback mit dem Grund des Pausenendes.</param>
    void Show(BreakModel model, TimeSpan breakDuration, string overlayColorHex, bool darkenAllScreens, Action<BreakEndReason> onEnded);

    /// <summary>Schließt offene Overlays ohne den Ende-Callback auszulösen.</summary>
    void Close();
}
