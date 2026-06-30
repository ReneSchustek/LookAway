using LookAway.Core.Enums;

namespace LookAway.Core.Events;

/// <summary>
/// Trägt die per globalem Hotkey ausgelöste Aktion.
/// </summary>
public sealed class HotkeyPressedEventArgs : EventArgs
{
    /// <summary>
    /// Erzeugt die Ereignisdaten.
    /// </summary>
    /// <param name="action">Die ausgelöste Aktion.</param>
    public HotkeyPressedEventArgs(HotkeyAction action)
    {
        Action = action;
    }

    /// <summary>Die ausgelöste Aktion.</summary>
    public HotkeyAction Action { get; }
}
