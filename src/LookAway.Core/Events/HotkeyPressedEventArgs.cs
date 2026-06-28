using LookAway.Core.Enums;

namespace LookAway.Core.Events;

/// <summary>
/// Traegt die per globalem Hotkey ausgeloeste Aktion (BRIEF019).
/// </summary>
public sealed class HotkeyPressedEventArgs : EventArgs
{
    /// <summary>
    /// Erzeugt die Ereignisdaten.
    /// </summary>
    /// <param name="action">Die ausgeloeste Aktion.</param>
    public HotkeyPressedEventArgs(HotkeyAction action)
    {
        Action = action;
    }

    /// <summary>Die ausgeloeste Aktion.</summary>
    public HotkeyAction Action { get; }
}
