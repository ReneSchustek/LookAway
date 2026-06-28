namespace LookAway.Core.Enums;

/// <summary>
/// Per globalem Hotkey ausloesbare Aktionen.
/// </summary>
public enum HotkeyAction
{
    /// <summary>Pause sofort starten.</summary>
    StartBreak,

    /// <summary>Aktuelle Erinnerung ueberspringen / verschieben.</summary>
    SkipOrSnooze,

    /// <summary>Nicht-stoeren-Modus umschalten.</summary>
    ToggleDnd,
}
