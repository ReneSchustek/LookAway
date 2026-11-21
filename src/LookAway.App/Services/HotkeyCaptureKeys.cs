using LookAway.Core.Enums;
using Windows.System;

namespace LookAway.App.Services;

/// <summary>Tastenwissen für die Aufnahme eines Hotkeys in den Einstellungen.</summary>
internal static class HotkeyCaptureKeys
{
    /// <summary>
    /// Ob die Taste nur ein Modifikator ist. Solche Tasten schließen eine Aufnahme nicht ab; erst
    /// die Taste, die zu ihnen gedrückt wird, ergibt die Kombination.
    /// </summary>
    /// <param name="key">Gedrückte Taste.</param>
    /// <returns><c>true</c> für Strg, Alt, Umschalt und Windows, links wie rechts.</returns>
    public static bool IsModifier(VirtualKey key) => key is VirtualKey.Control
        or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
        or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
        or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    /// <summary>Fasst die gerade gehaltenen Modifikatoren zusammen.</summary>
    /// <param name="isDown">Ob eine Taste gerade gehalten wird.</param>
    /// <returns>Die gehaltenen Modifikatoren; die Windows-Taste zählt links wie rechts.</returns>
    public static HotkeyModifiers ReadModifiers(Func<VirtualKey, bool> isDown)
    {
        ArgumentNullException.ThrowIfNull(isDown);

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        if (isDown(VirtualKey.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (isDown(VirtualKey.Menu))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (isDown(VirtualKey.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (isDown(VirtualKey.LeftWindows) || isDown(VirtualKey.RightWindows))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        return modifiers;
    }
}
