using LookAway.Core.Interfaces;

namespace LookAway.Application.Services;

/// <summary>
/// Unterdrückt Pause-Erinnerungen, solange eine Vollbild-Anwendung läuft
/// (DND), und merkt sich maximal eine verpasste Erinnerung, die nach Verlassen
/// des Vollbildmodus nachgeholt wird. Reine Logik über
/// <see cref="IFullscreenDetector"/> — ohne Plattform-/UI-Abhängigkeit testbar.
/// </summary>
public sealed class FullscreenDetectionService
{
    private readonly IFullscreenDetector _fullscreenDetector;
    private bool _missedReminderPending;

    /// <summary>
    /// Erzeugt den Dienst mit der Vollbild-Quelle.
    /// </summary>
    /// <param name="fullscreenDetector">Quelle der Vollbild-Erkennung.</param>
    public FullscreenDetectionService(IFullscreenDetector fullscreenDetector)
    {
        ArgumentNullException.ThrowIfNull(fullscreenDetector);
        _fullscreenDetector = fullscreenDetector;
        IsEnabled = true;
    }

    /// <summary>Ist die Vollbild-Unterdrückung (DND) aktiv?</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Wahr, solange eine Vollbild-App erkannt und DND aktiviert ist.</summary>
    public bool IsDndActive { get; private set; }

    /// <summary>Wahr, wenn eine verpasste Erinnerung auf Nachholung wartet.</summary>
    public bool HasPendingReminder => _missedReminderPending;

    /// <summary>
    /// Aktualisiert den DND-Zustand anhand der Vollbild-Erkennung. Pro Polling-
    /// Intervall einmal aufzurufen.
    /// </summary>
    /// <returns>
    /// <c>true</c>, wenn DND gerade beendet wurde und eine verpasste Erinnerung
    /// jetzt nachzuholen ist; sonst <c>false</c>.
    /// </returns>
    public bool Evaluate()
    {
        bool active = IsEnabled && _fullscreenDetector.IsFullscreenApplicationActive();
        bool justDeactivated = IsDndActive && !active;
        IsDndActive = active;

        if (justDeactivated && _missedReminderPending)
        {
            _missedReminderPending = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Meldet eine fällige Erinnerung an. Ist DND aktiv, wird sie unterdrückt
    /// und (höchstens eine) für später gemerkt.
    /// </summary>
    /// <returns><c>true</c>, wenn die Erinnerung jetzt gezeigt werden darf; sonst <c>false</c>.</returns>
    public bool TryShowReminder()
    {
        if (IsDndActive)
        {
            _missedReminderPending = true;
            return false;
        }

        return true;
    }
}
