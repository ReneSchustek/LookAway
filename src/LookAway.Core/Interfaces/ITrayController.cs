using LookAway.Core.Enums;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Schmale Sicht auf das Tray-Icon, soweit der Pausen-Koordinator es steuert
/// (aktives Modell und Nicht-stören-Anzeige).
/// </summary>
public interface ITrayController
{
    /// <summary>Setzt das angezeigte aktive Pausenmodell.</summary>
    /// <param name="model">Aktives Modell.</param>
    void SetActiveModel(BreakModel model);

    /// <summary>Schaltet die Nicht-stören-Anzeige um.</summary>
    /// <param name="isDndActive">Ist DND aktiv?</param>
    void SetDndActive(bool isDndActive);
}
