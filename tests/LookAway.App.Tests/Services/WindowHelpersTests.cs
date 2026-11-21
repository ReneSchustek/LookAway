using LookAway.App.Services;
using LookAway.Core.Enums;
using Windows.System;

namespace LookAway.App.Tests;

/// <summary>
/// Kleine Bausteine der Fenster: die mittige Lage im Arbeitsbereich und das Tastenwissen der
/// Hotkey-Aufnahme.
/// </summary>
public sealed class WindowHelpersTests
{
    /// <remarks>Der Arbeitsbereich beginnt nicht bei null, wenn die Taskleiste links oder oben steht.</remarks>
    [Fact]
    public void CenterIn_PlacesTheWindowInTheMiddleOfTheWorkArea()
        => Assert.Equal((100 + 260, 48 + 196), WindowPlacement.CenterIn(100, 48, 1000, 1000, 480, 608));

    [Fact]
    public void CenterIn_WithAWindowLargerThanTheArea_StartsBeforeIt()
        => Assert.Equal((-20, 0), WindowPlacement.CenterIn(0, 0, 800, 600, 840, 600));

    [Theory]
    [InlineData(VirtualKey.Control)]
    [InlineData(VirtualKey.LeftControl)]
    [InlineData(VirtualKey.RightMenu)]
    [InlineData(VirtualKey.Shift)]
    [InlineData(VirtualKey.LeftWindows)]
    public void ModifierKeys_DoNotCompleteACapture(VirtualKey key)
        => Assert.True(HotkeyCaptureKeys.IsModifier(key));

    [Theory]
    [InlineData(VirtualKey.P)]
    [InlineData(VirtualKey.F9)]
    [InlineData(VirtualKey.Escape)]
    public void OtherKeys_AreNoModifiers(VirtualKey key)
        => Assert.False(HotkeyCaptureKeys.IsModifier(key));

    [Fact]
    public void ReadModifiers_CombinesEveryHeldModifier()
    {
        HashSet<VirtualKey> held = [VirtualKey.Control, VirtualKey.Menu, VirtualKey.Shift, VirtualKey.RightWindows];

        HotkeyModifiers modifiers = HotkeyCaptureKeys.ReadModifiers(held.Contains);

        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, modifiers);
    }

    [Fact]
    public void ReadModifiers_WithoutHeldKeys_IsNone()
        => Assert.Equal(HotkeyModifiers.None, HotkeyCaptureKeys.ReadModifiers(_ => false));
}
