using LookAway.Core.Enums;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Zustand und Aktionslogik der Pause-Erinnerung — UI-frei und damit ohne WinUI
/// testbar. Die View (App-Schicht) bindet daran und ruft bei Timeout
/// <see cref="TimeoutElapsed"/> auf.
/// </summary>
/// <remarks>
/// Die erste gewaehlte Aktion gewinnt; weitere Aufrufe (auch ein spaeter Timeout)
/// werden ignoriert. So koennen Doppelklicks oder ein Timeout-Race die Aktion
/// nicht ueberschreiben.
/// </remarks>
public sealed class BreakReminderViewModel
{
    /// <summary>Sekunden bis zur automatischen Default-Aktion.</summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>Verschiebedauer in Minuten bei "Snooze".</summary>
    public const int SnoozeMinutes = 5;

    /// <summary>Lokalisierungs-Schluessel des Titels.</summary>
    public const string TitleKey = "Reminder.Title";

    private bool _completed;

    /// <summary>
    /// Erzeugt das ViewModel fuer ein Pausenmodell.
    /// </summary>
    /// <param name="hintKey">Lokalisierungs-Schluessel des Uebungs-Hinweises.</param>
    public BreakReminderViewModel(string hintKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hintKey);
        HintKey = hintKey;
    }

    /// <summary>Wird ausgeloest, sobald eine Aktion feststeht (genau einmal).</summary>
    public event EventHandler<ReminderCompletedEventArgs>? Completed;

    /// <summary>Lokalisierungs-Schluessel des angezeigten Uebungs-Hinweises.</summary>
    public string HintKey { get; }

    /// <summary>Wahr, sobald eine Aktion gewaehlt wurde.</summary>
    public bool IsCompleted => _completed;

    /// <summary>Die gewaehlte Aktion, oder <c>null</c> solange offen.</summary>
    public ReminderResult? Result { get; private set; }

    /// <summary>Aktion "Pause starten".</summary>
    public void StartBreak() => Complete(ReminderResult.StartBreak);

    /// <summary>Aktion "5 Min spaeter" (Snooze).</summary>
    public void Snooze() => Complete(ReminderResult.Snooze);

    /// <summary>Aktion "Ueberspringen".</summary>
    public void Skip() => Complete(ReminderResult.Skip);

    /// <summary>
    /// Wird vom UI-Timeout aufgerufen. Ohne vorherige Benutzeraktion gilt die
    /// Default-Aktion <see cref="ReminderResult.StartBreak"/>.
    /// </summary>
    public void TimeoutElapsed()
    {
        if (!_completed)
        {
            Complete(ReminderResult.StartBreak);
        }
    }

    private void Complete(ReminderResult result)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Result = result;
        Completed?.Invoke(this, new ReminderCompletedEventArgs(result));
    }
}
