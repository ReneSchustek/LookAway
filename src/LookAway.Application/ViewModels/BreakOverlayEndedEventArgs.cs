using LookAway.Core.Enums;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Argumente zum Ende einer laufenden Pause.
/// </summary>
public sealed class BreakOverlayEndedEventArgs : EventArgs
{
    /// <summary>Erzeugt die Argumente für den angegebenen Grund.</summary>
    /// <param name="reason">Grund des Pausenendes.</param>
    public BreakOverlayEndedEventArgs(BreakEndReason reason) => Reason = reason;

    /// <summary>Grund, aus dem die Pause beendet wurde.</summary>
    public BreakEndReason Reason { get; }
}
