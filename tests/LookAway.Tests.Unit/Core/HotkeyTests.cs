using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core;

/// <summary>
/// Tests für die Hotkey-Validierung, -Defaults und -Darstellung.
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
    public void FindConflicts_meldet_ungültige_Leer_Bindungen_nicht()
    {
        // Zwei ungebundene Aktionen (None+0) sind wertgleich, dürfen aber nicht
        // als Konflikt gelten.
        Dictionary<HotkeyAction, HotkeyDefinition> bindings = new()
        {
            [HotkeyAction.StartBreak] = new HotkeyDefinition(HotkeyModifiers.None, 0),
            [HotkeyAction.SkipOrSnooze] = new HotkeyDefinition(HotkeyModifiers.None, 0),
            [HotkeyAction.ToggleDnd] = HotkeyDefaults.ToggleDnd,
        };

        Assert.Empty(HotkeyValidator.FindConflicts(bindings));
    }

    [Fact]
    public void Defaults_sind_gültig_und_kollisionsfrei()
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

    [Fact]
    public void Format_nutzt_die_übergebenen_Modifikatornamen()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkP);

        string text = definition.Format(modifier => modifier switch
        {
            HotkeyModifiers.Control => "Ctrl",
            HotkeyModifiers.Shift => "Shift",
            _ => "?",
        });

        Assert.Equal("Ctrl+Shift+P", text);
    }

    [Fact]
    public void KeyLabel_ist_leer_ohne_Taste()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control, 0);

        Assert.Equal(string.Empty, definition.KeyLabel);
    }
}
