using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core;

/// <summary>
/// Tests fuer die Hotkey-Validierung, -Defaults und -Darstellung.
/// </summary>
public sealed class HotkeyTests
{
    private const int VkP = 0x50;

    [Fact]
    public void IsValid_akzeptiert_Modifikator_plus_Taste()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkP);

        Assert.True(HotkeyValidator.IsValid(definition));
    }

    [Fact]
    public void IsValid_lehnt_Taste_ohne_Modifikator_ab()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.None, VkP);

        Assert.False(HotkeyValidator.IsValid(definition));
    }

    [Fact]
    public void IsValid_lehnt_nur_Umschalt_ab()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Shift, VkP);

        Assert.False(HotkeyValidator.IsValid(definition));
    }

    [Fact]
    public void FindConflicts_erkennt_doppelte_Belegung()
    {
        HotkeyDefinition shared = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkP);
        Dictionary<HotkeyAction, HotkeyDefinition> bindings = new()
        {
            [HotkeyAction.StartBreak] = shared,
            [HotkeyAction.SkipOrSnooze] = shared,
            [HotkeyAction.ToggleDnd] = HotkeyDefaults.ToggleDnd,
        };

        IReadOnlyCollection<HotkeyAction> conflicts = HotkeyValidator.FindConflicts(bindings);

        Assert.Contains(HotkeyAction.StartBreak, conflicts);
        Assert.Contains(HotkeyAction.SkipOrSnooze, conflicts);
        Assert.DoesNotContain(HotkeyAction.ToggleDnd, conflicts);
    }

    [Fact]
    public void Defaults_sind_gueltig_und_kollisionsfrei()
    {
        IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> defaults = HotkeyDefaults.CreateDefaults();

        Assert.All(defaults.Values, definition => Assert.True(HotkeyValidator.IsValid(definition)));
        Assert.Empty(HotkeyValidator.FindConflicts(defaults));
    }

    [Fact]
    public void ToString_zeigt_lesbare_Kombination()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkP);

        Assert.Equal("Strg+Alt+P", definition.ToString());
    }
}
