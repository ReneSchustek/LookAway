using LookAway.Core.Interfaces;

namespace LookAway.Core.Services;

/// <summary>
/// Unterdrückt Pause-Erinnerungen, solange eine Vollbild-Anwendung läuft
/// (DND), und merkt sich maximal eine verpasste Erinnerung, die nach Verlassen
/// des Vollbildmodus nachgeholt wird. Reine Logik über
/// <see cref="IFullscreenDetector"/> — ohne Plattform-/UI-Abhängigkeit testbar.
/// </summary>
/// <remarks>
/// <see cref="Evaluate"/> läuft im Hintergrund-Erkennungs-Loop, <see cref="TryShowReminder"/>
/// dagegen auf dem UI-Thread beim Eintreffen eines fälligen Erinnerungs-Ereignisses.
/// Der gemeinsame Zustand (<see cref="IsDndActive"/>, ausstehende Erinnerung) wird
/// deshalb unter einem gemeinsamen Lock gehalten.
/// </remarks>
public sealed class FullscreenDetectionService
{
    private readonly IFullscreenDetector _fullscreenDetector;
    private readonly Lock _gate = new();
    private bool _missedReminderPending;
    private bool _isDndActive;

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
    public bool IsDndActive
    {
        get
        {
            lock (_gate)
            {
                return _isDndActive;
            }
        }
    }

    /// <summary>Wahr, wenn eine verpasste Erinnerung auf Nachholung wartet.</summary>
    public bool HasPendingReminder
    {
        get
        {
            lock (_gate)
            {
                return _missedReminderPending;
            }
        }
    }

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
        // Die Detektor-Abfrage bewusst außerhalb des Locks — sie kann Win32-Aufrufe
        // machen und darf den UI-Thread in TryShowReminder nicht blockieren.
        bool active = IsEnabled && _fullscreenDetector.IsFullscreenApplicationActive();

        lock (_gate)
        {
            bool justDeactivated = _isDndActive && !active;
            _isDndActive = active;

            if (justDeactivated && _missedReminderPending)
            {
                _missedReminderPending = false;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Meldet eine fällige Erinnerung an. Ist DND aktiv, wird sie unterdrückt
    /// und (höchstens eine) für später gemerkt.
    /// </summary>
    /// <returns><c>true</c>, wenn die Erinnerung jetzt gezeigt werden darf; sonst <c>false</c>.</returns>
    public bool TryShowReminder()
    {
        lock (_gate)
        {
            if (_isDndActive)
            {
                _missedReminderPending = true;
                return false;
            }

            return true;
        }
    }
}
