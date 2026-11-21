using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests;

/// <summary>
/// Die globalen Hotkeys: Belegung aus den Einstellungen, Freigabe während einer Aufnahme und die
/// Aktion zu jeder Taste.
/// </summary>
public sealed class HotkeyBindingsTests : IDisposable
{
    private readonly FakeHotkeyService _hotkeys = new();
    private readonly ImmediateUiDispatcher _dispatcher = new();
    private readonly List<string> _actions = [];
    private readonly HotkeyBindings _bindings;

    public HotkeyBindingsTests()
        => _bindings = new HotkeyBindings(
            _hotkeys,
            _dispatcher,
            () => _actions.Add("break"),
            () => _actions.Add("skip"),
            () => _actions.Add("dnd"));

    public void Dispose() => _bindings.Dispose();

    [Fact]
    public void Apply_RegistersTheThreeKeysFromTheSettings()
    {
        Settings settings = new() { HotkeysEnabled = true };

        _bindings.Apply(settings);

        IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> registered = Assert.IsAssignableFrom<IReadOnlyDictionary<HotkeyAction, HotkeyDefinition>>(_hotkeys.Registered);
        Assert.Equal(settings.HotkeyStartBreak, registered[HotkeyAction.StartBreak]);
        Assert.Equal(settings.HotkeySkipOrSnooze, registered[HotkeyAction.SkipOrSnooze]);
        Assert.Equal(settings.HotkeyToggleDnd, registered[HotkeyAction.ToggleDnd]);
    }

    [Fact]
    public void Apply_WithHotkeysSwitchedOff_FreesAllKeys()
    {
        _bindings.Apply(new Settings { HotkeysEnabled = true });

        _bindings.Apply(new Settings { HotkeysEnabled = false });

        Assert.Null(_hotkeys.Registered);
    }

    /// <remarks>
    /// Während der Aufnahme sind alle Tasten frei, sonst fängt Windows genau die Kombination ab,
    /// die aufgenommen werden soll. Danach gilt wieder die zuletzt übernommene Belegung.
    /// </remarks>
    [Fact]
    public void Capture_FreesTheKeysAndRestoresThemAfterwards()
    {
        _bindings.Apply(new Settings { HotkeysEnabled = true });

        _bindings.SetCaptureRunning(true);
        Assert.Null(_hotkeys.Registered);

        _bindings.SetCaptureRunning(false);
        Assert.NotNull(_hotkeys.Registered);
    }

    [Fact]
    public void CaptureEnd_WithoutEarlierSettings_RegistersNothing()
    {
        _bindings.SetCaptureRunning(false);

        Assert.Null(_hotkeys.Registered);
        Assert.Equal(0, _hotkeys.UnregisterCount);
    }

    [Fact]
    public void EveryKey_TriggersItsActionOnTheUiThread()
    {
        _hotkeys.Press(HotkeyAction.StartBreak);
        _hotkeys.Press(HotkeyAction.SkipOrSnooze);
        _hotkeys.Press(HotkeyAction.ToggleDnd);
        _hotkeys.Press((HotkeyAction)42);

        Assert.Equal(["break", "skip", "dnd"], _actions);
        Assert.Equal(4, _dispatcher.PostCount);
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        _bindings.Dispose();
        _bindings.Dispose();

        Assert.False(_hotkeys.HasSubscribers);
    }
}
