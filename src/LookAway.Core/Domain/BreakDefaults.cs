namespace LookAway.Core.Domain;

/// <summary>
/// Feste Vorgaben des Pausenablaufs, die unabhängig vom gewählten Pausenmodell gelten.
/// </summary>
public static class BreakDefaults
{
    /// <summary>
    /// Arbeitsdauer bis zur nächsten Erinnerung, wenn der Benutzer eine Erinnerung
    /// zurückstellt. Entspricht der Untergrenze von <see cref="ValueObjects.BreakInterval"/>.
    /// </summary>
    public static readonly TimeSpan SnoozeWorkDuration = TimeSpan.FromMinutes(5);
}
