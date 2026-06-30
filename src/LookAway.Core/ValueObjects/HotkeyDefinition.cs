using LookAway.Core.Enums;

namespace LookAway.Core.ValueObjects;

/// <summary>
/// Definition eines globalen Hotkeys: Modifikatoren plus virtueller Tastencode.
/// Unveränderlich; <see cref="VirtualKey"/> ist ein Win32-VK-Code.
/// </summary>
/// <param name="Modifiers">Modifikatortasten.</param>
/// <param name="VirtualKey">Virtueller Tastencode (Win32 VK).</param>
public readonly record struct HotkeyDefinition(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>Wahr, wenn mindestens ein Modifikator und eine Taste gesetzt sind.</summary>
    public bool HasKey => Modifiers != HotkeyModifiers.None && VirtualKey != 0;

    /// <summary>
    /// Sprachneutrale Beschriftung der Taste ohne Modifikatoren (z. B. <c>"P"</c>,
    /// <c>"F1"</c>); leer, wenn keine Taste gesetzt ist.
    /// </summary>
    public string KeyLabel => VirtualKey == 0 ? string.Empty : KeyName(VirtualKey);

    /// <summary>
    /// Setzt die lesbare Tastenkombination aus den Modifikatornamen und der Taste
    /// zusammen, z. B. <c>"Strg+Alt+P"</c>. Die Modifikatornamen liefert
    /// <paramref name="modifierName"/>, sodass die Darstellung sprachabhängig
    /// gerendert werden kann.
    /// </summary>
    /// <param name="modifierName">
    /// Liefert den Anzeigenamen eines einzelnen Modifikator-Flags
    /// (<see cref="HotkeyModifiers.Control"/>/<see cref="HotkeyModifiers.Alt"/>/
    /// <see cref="HotkeyModifiers.Shift"/>/<see cref="HotkeyModifiers.Win"/>).
    /// </param>
    /// <returns>Die Tastenkombination als Text; <c>"—"</c>, wenn nichts gesetzt ist.</returns>
    public string Format(Func<HotkeyModifiers, string> modifierName)
    {
        ArgumentNullException.ThrowIfNull(modifierName);

        List<string> parts = new();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add(modifierName(HotkeyModifiers.Control));
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add(modifierName(HotkeyModifiers.Alt));
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add(modifierName(HotkeyModifiers.Shift));
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add(modifierName(HotkeyModifiers.Win));
        }

        string key = KeyLabel;
        if (key.Length != 0)
        {
            parts.Add(key);
        }

        return parts.Count == 0 ? "—" : string.Join("+", parts);
    }

    /// <summary>
    /// Liefert eine lesbare Darstellung mit deutschen Modifikatornamen, z. B.
    /// <c>"Strg+Alt+P"</c>. Dient als sprachneutraler Rückfall (Logging/Diagnose);
    /// die Oberfläche rendert die Kombination lokalisiert über
    /// <see cref="Format(Func{HotkeyModifiers, string})"/>.
    /// </summary>
    /// <returns>Die Tastenkombination als Text.</returns>
    public override string ToString() => Format(static modifier => modifier switch
    {
        HotkeyModifiers.Control => "Strg",
        HotkeyModifiers.Alt => "Alt",
        HotkeyModifiers.Shift => "Umschalt",
        HotkeyModifiers.Win => "Win",
        _ => string.Empty,
    });

    private static string KeyName(int virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),       // A–Z
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),       // 0–9
        >= 0x70 and <= 0x7B => "F" + (virtualKey - 0x6F),           // F1–F12
        _ => "VK" + virtualKey,
    };
}
