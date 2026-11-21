using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Services;

/// <summary>
/// Verbindet die globalen Hotkeys mit dem Pausen-Ablauf: registriert die Belegung aus den
/// Einstellungen, gibt die Tasten während einer Aufnahme frei und leitet einen Tastendruck auf den
/// Fenster-Thread an die passende Aktion weiter.
/// </summary>
internal sealed class HotkeyBindings : IDisposable
{
    private readonly IHotkeyService _hotkeys;
    private readonly IUiDispatcher _dispatcher;
    private readonly Action _startBreak;
    private readonly Action _skipOrSnooze;
    private readonly Action _toggleDnd;
    private Settings? _current;
    private bool _disposed;

    /// <summary>Meldet sich beim Hotkey-Dienst an.</summary>
    /// <param name="hotkeys">Registriert die Tasten bei Windows.</param>
    /// <param name="dispatcher">Wechselt für die Aktionen auf den Fenster-Thread.</param>
    /// <param name="startBreak">Aktion zu „Pause jetzt“.</param>
    /// <param name="skipOrSnooze">Aktion zu „Überspringen oder später“.</param>
    /// <param name="toggleDnd">Aktion zu „Nicht stören“ an oder aus.</param>
    public HotkeyBindings(
        IHotkeyService hotkeys,
        IUiDispatcher dispatcher,
        Action startBreak,
        Action skipOrSnooze,
        Action toggleDnd)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(startBreak);
        ArgumentNullException.ThrowIfNull(skipOrSnooze);
        ArgumentNullException.ThrowIfNull(toggleDnd);
        _hotkeys = hotkeys;
        _dispatcher = dispatcher;
        _startBreak = startBreak;
        _skipOrSnooze = skipOrSnooze;
        _toggleDnd = toggleDnd;

        _hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    /// <summary>
    /// Übernimmt die Belegung aus den Einstellungen: registriert die drei Tasten oder gibt alle
    /// frei, wenn Hotkeys ausgeschaltet sind.
    /// </summary>
    /// <param name="settings">Aktuelle Einstellungen.</param>
    public void Apply(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _current = settings;

        if (!settings.HotkeysEnabled)
        {
            _hotkeys.UnregisterAll();
            return;
        }

        Dictionary<HotkeyAction, HotkeyDefinition> bindings = new()
        {
            [HotkeyAction.StartBreak] = settings.HotkeyStartBreak,
            [HotkeyAction.SkipOrSnooze] = settings.HotkeySkipOrSnooze,
            [HotkeyAction.ToggleDnd] = settings.HotkeyToggleDnd,
        };
        _hotkeys.Register(bindings);
    }

    /// <summary>
    /// Beginn oder Ende einer Aufnahme in den Einstellungen. Während der Aufnahme sind alle Tasten
    /// frei, sonst finge Windows genau die Kombination ab, die aufgenommen werden soll; danach gilt
    /// wieder die zuletzt übernommene Belegung.
    /// </summary>
    /// <param name="captureRunning">Läuft gerade eine Aufnahme?</param>
    public void SetCaptureRunning(bool captureRunning)
    {
        if (captureRunning)
        {
            _hotkeys.UnregisterAll();
            return;
        }

        if (_current is not null)
        {
            Apply(_current);
        }
    }

    /// <summary>Meldet sich vom Hotkey-Dienst ab.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
        => _dispatcher.Post(() => Handle(e.Action));

    private void Handle(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.StartBreak:
                _startBreak();
                break;
            case HotkeyAction.SkipOrSnooze:
                _skipOrSnooze();
                break;
            case HotkeyAction.ToggleDnd:
                _toggleDnd();
                break;
            default:
                // Eine Taste ohne bekannte Aktion bleibt folgenlos.
                break;
        }
    }
}
