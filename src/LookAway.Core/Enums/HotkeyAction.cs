namespace LookAway.Core.Enums;

/// <summary>
/// Per globalem Hotkey auslösbare Aktionen.
/// </summary>
public enum HotkeyAction
{
    /// <summary>Pause sofort starten.</summary>
    StartBreak,

    /// <summary>Aktuelle Erinnerung überspringen / verschieben.</summary>
    SkipOrSnooze,

    /// <summary>Nicht-stören-Modus umschalten.</summary>
    ToggleDnd,
}
