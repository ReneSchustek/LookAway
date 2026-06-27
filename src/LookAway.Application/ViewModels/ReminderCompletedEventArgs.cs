using LookAway.Core.Enums;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Ereignisdaten fuer den Abschluss einer Pause-Erinnerung.
/// </summary>
/// <param name="chosenAction">Die gewaehlte Aktion.</param>
public sealed class ReminderCompletedEventArgs(ReminderResult chosenAction) : EventArgs
{
    /// <summary>Die vom Benutzer oder per Timeout gewaehlte Aktion.</summary>
    public ReminderResult ChosenAction { get; } = chosenAction;
}
