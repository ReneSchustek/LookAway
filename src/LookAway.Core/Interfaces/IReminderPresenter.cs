using LookAway.Core.Enums;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Zeigt das Pause-Erinnerungsfenster. UI-freie Abstraktion, damit der
/// Pausen-Koordinator ohne WinUI-Abhängigkeit testbar bleibt.
/// </summary>
public interface IReminderPresenter
{
    /// <summary>Ist gerade eine Erinnerung sichtbar?</summary>
    bool IsReminderOpen { get; }

    /// <summary>Zeigt eine Erinnerung; bei bereits offener passiert nichts.</summary>
    /// <param name="model">Aktives Pausenmodell (für den Hinweistext).</param>
    /// <param name="autoStartAfter">
    /// Verzögerung, nach der die Pause ohne Benutzeraktion automatisch startet, oder
    /// <c>null</c>, wenn die Erinnerung bis zu einer Benutzeraktion offen bleiben soll.
    /// </param>
    /// <param name="onResult">Callback mit der gewählten Aktion.</param>
    void Show(BreakModel model, TimeSpan? autoStartAfter, Action<ReminderResult> onResult);
}
