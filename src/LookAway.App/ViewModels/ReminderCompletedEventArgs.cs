using LookAway.Core.Enums;

namespace LookAway.App.ViewModels;

/// <summary>
/// Ereignisdaten für den Abschluss einer Pause-Erinnerung.
/// </summary>
/// <param name="chosenAction">Die gewählte Aktion.</param>
internal sealed class ReminderCompletedEventArgs(ReminderResult chosenAction) : EventArgs
{
    /// <summary>Die vom Benutzer oder per Timeout gewählte Aktion.</summary>
    public ReminderResult ChosenAction { get; } = chosenAction;
}
