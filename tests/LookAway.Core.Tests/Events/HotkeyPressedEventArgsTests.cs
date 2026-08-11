using LookAway.Core.Enums;
using LookAway.Core.Events;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für <see cref="HotkeyPressedEventArgs"/>.
/// </summary>
public sealed class HotkeyPressedEventArgsTests
{
    /// <remarks>
    /// Ausgelöst wird das Ereignis von der Nachrichtenschleife des Hotkey-Dienstes,
    /// die den Win32-Tastencode zurück in eine Aktion übersetzt. Diese Zuordnung ist
    /// die einzige Stelle, an der ein Vertippen zu einer falschen Aktion führen würde
    /// — deshalb hält der Test fest, dass die Aktion unverändert ankommt.
    /// </remarks>
    [Theory]
    [InlineData(HotkeyAction.StartBreak)]
    [InlineData(HotkeyAction.SkipOrSnooze)]
    [InlineData(HotkeyAction.ToggleDnd)]
    public void CarriesTheActionUnchanged(HotkeyAction action)
    {
        HotkeyPressedEventArgs args = new(action);

        Assert.Equal(action, args.Action);
    }
}
