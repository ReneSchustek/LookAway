using LookAway.Core.Enums;

namespace LookAway.Core.ValueObjects;

/// <summary>
/// Definition eines globalen Hotkeys: Modifikatoren plus virtueller Tastencode
/// (BRIEF019). Unveraenderlich; <see cref="VirtualKey"/> ist ein Win32-VK-Code.
/// </summary>
/// <param name="Modifiers">Modifikatortasten.</param>
/// <param name="VirtualKey">Virtueller Tastencode (Win32 VK).</param>
public readonly record struct HotkeyDefinition(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>Wahr, wenn mindestens ein Modifikator und eine Taste gesetzt sind.</summary>
    public bool HasKey => Modifiers != HotkeyModifiers.None && VirtualKey != 0;

    /// <summary>
    /// Liefert eine lesbare Darstellung, z. B. <c>"Strg+Alt+P"</c>.
    /// </summary>
    /// <returns>Die Tastenkombination als Text.</returns>
    public override string ToString()
    {
        List<string> parts = new();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Strg");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Umschalt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        if (VirtualKey != 0)
        {
            parts.Add(KeyName(VirtualKey));
        }

        return parts.Count == 0 ? "—" : string.Join("+", parts);
    }

    private static string KeyName(int virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),       // A–Z
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),       // 0–9
        >= 0x70 and <= 0x7B => "F" + (virtualKey - 0x6F),           // F1–F12
        _ => "VK" + virtualKey,
    };
}
