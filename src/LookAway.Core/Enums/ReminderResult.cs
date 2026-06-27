namespace LookAway.Core.Enums;

/// <summary>
/// Die vom Benutzer (oder per Timeout) gewaehlte Reaktion auf eine
/// Pause-Erinnerung.
/// </summary>
public enum ReminderResult
{
    /// <summary>Pause jetzt beginnen.</summary>
    StartBreak,

    /// <summary>Erinnerung um wenige Minuten verschieben.</summary>
    Snooze,

    /// <summary>Pause ueberspringen, naechster Arbeitszyklus beginnt.</summary>
    Skip,
}
