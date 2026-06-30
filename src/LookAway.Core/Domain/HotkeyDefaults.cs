using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Domain;

/// <summary>
/// Standard-Tastenkombinationen der globalen Hotkeys.
/// </summary>
public static class HotkeyDefaults
{
    private const int VkP = 0x50;
    private const int VkS = 0x53;
    private const int VkD = 0x44;

    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    /// <summary>Standard für "Pause starten" (Strg+Alt+P).</summary>
    public static HotkeyDefinition StartBreak => new(CtrlAlt, VkP);

    /// <summary>Standard für "Überspringen/Snooze" (Strg+Alt+S).</summary>
    public static HotkeyDefinition SkipOrSnooze => new(CtrlAlt, VkS);

    /// <summary>Standard für "DND umschalten" (Strg+Alt+D).</summary>
    public static HotkeyDefinition ToggleDnd => new(CtrlAlt, VkD);

    /// <summary>Liefert die Standardzuordnung aller Aktionen.</summary>
    /// <returns>Aktion → Standard-Tastenkombination.</returns>
    public static IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> CreateDefaults()
        => new Dictionary<HotkeyAction, HotkeyDefinition>
        {
            [HotkeyAction.StartBreak] = StartBreak,
            [HotkeyAction.SkipOrSnooze] = SkipOrSnooze,
            [HotkeyAction.ToggleDnd] = ToggleDnd,
        };
}
