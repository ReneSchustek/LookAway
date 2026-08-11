using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für die Hotkey-Validierung, -Defaults und -Darstellung.
/// </summary>
public sealed class HotkeyTests
{
    private const int VkP = 0x50;

    [Fact]
    public void IsValid_AcceptsAModifierPlusKey()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkP);

        Assert.True(HotkeyValidator.IsValid(definition));
    }

    [Fact]
    public void IsValid_RejectsAKeyWithoutModifier()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.None, VkP);

        Assert.False(HotkeyValidator.IsValid(definition));
    }

    [Fact]
    public void IsValid_RejectsShiftOnly()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Shift, VkP);

        Assert.False(HotkeyValidator.IsValid(definition));
    }

    [Fact]
    public void FindConflicts_DetectsADuplicateAssignment()
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
    public void FindConflicts_IgnoresInvalidEmptyBindings()
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
    public void Defaults_AreValidAndFreeOfConflicts()
    {
        IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> defaults = HotkeyDefaults.CreateDefaults();

        Assert.All(defaults.Values, definition => Assert.True(HotkeyValidator.IsValid(definition)));
        Assert.Empty(HotkeyValidator.FindConflicts(defaults));
    }

    [Fact]
    public void ToString_ShowsAReadableCombination()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkP);

        Assert.Equal("Strg+Alt+P", definition.ToString());
    }

    [Fact]
    public void Format_UsesTheSuppliedModifierNames()
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
    public void KeyLabel_WithoutKey_IsEmpty()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control, 0);

        Assert.Equal(string.Empty, definition.KeyLabel);
    }

    /// <remarks>
    /// Die Reihenfolge liegt fest und folgt der Beschriftung auf der Tastatur —
    /// „Win+Umschalt+P" läse sich verkehrt herum.
    /// </remarks>
    [Fact]
    public void ToString_ListsAllFourModifiersInFixedOrder()
    {
        HotkeyDefinition definition = new(
            HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win,
            VkP);

        Assert.Equal("Strg+Alt+Umschalt+Win+P", definition.ToString());
    }

    [Fact]
    public void Format_PassesTheWindowsModifierThrough()
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Win, VkP);

        string text = definition.Format(modifier => modifier switch
        {
            HotkeyModifiers.Win => "Cmd",
            _ => "?",
        });

        Assert.Equal("Cmd+P", text);
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x30, "0")]
    [InlineData(0x39, "9")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    public void KeyLabel_NamesLettersDigitsAndFunctionKeys(int virtualKey, string expected)
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control, virtualKey);

        Assert.Equal(expected, definition.KeyLabel);
    }

    /// <remarks>
    /// Für alles Übrige bleibt der rohe Tastencode stehen. Das ist unschön zu lesen,
    /// aber ehrlicher als eine erfundene Beschriftung — und der Nutzer erkennt, dass
    /// er eine Taste erwischt hat, die hier niemand vorgesehen hat.
    /// </remarks>
    [Theory]
    [InlineData(0x2D)]
    [InlineData(0x7C)]
    public void KeyLabel_FallsBackToTheRawCode(int virtualKey)
    {
        HotkeyDefinition definition = new(HotkeyModifiers.Control, virtualKey);

        Assert.Equal("VK" + virtualKey, definition.KeyLabel);
    }
}
