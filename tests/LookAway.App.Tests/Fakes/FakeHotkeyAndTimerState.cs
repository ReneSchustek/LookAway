using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>Hotkey-Dienst ohne Windows: merkt sich die Belegung und lässt Tasten von Hand drücken.</summary>
internal sealed class FakeHotkeyService : IHotkeyService
{
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>Zuletzt registrierte Belegung; <c>null</c>, solange alles freigegeben ist.</summary>
    public IReadOnlyDictionary<HotkeyAction, HotkeyDefinition>? Registered { get; private set; }

    public int UnregisterCount { get; private set; }

    public bool HasSubscribers => HotkeyPressed is not null;

    public void Register(IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> bindings) => Registered = bindings;

    public void UnregisterAll()
    {
        UnregisterCount++;
        Registered = null;
    }

    /// <summary>Drückt eine Taste, wie es Windows melden würde.</summary>
    public void Press(HotkeyAction action) => HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(action));
}

/// <summary>Ablage der Momentaufnahme im Speicher.</summary>
internal sealed class FakeTimerStateStore : ITimerStateStore
{
    public TimerSnapshot? Stored { get; set; }

    public int ClearCount { get; private set; }

    public void Save(TimerSnapshot snapshot) => Stored = snapshot;

    public TimerSnapshot? Load() => Stored;

    public void Clear()
    {
        ClearCount++;
        Stored = null;
    }
}
