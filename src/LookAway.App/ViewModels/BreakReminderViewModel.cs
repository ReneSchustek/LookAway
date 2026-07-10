using LookAway.Core.Enums;
using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Zustand und Aktionslogik der Pause-Erinnerung — UI-frei und damit ohne WinUI
/// testbar. Die View (App-Schicht) bindet daran und ruft bei Timeout
/// <see cref="TimeoutElapsed"/> auf.
/// </summary>
/// <remarks>
/// Die erste gewählte Aktion gewinnt; weitere Aufrufe (auch ein später Timeout)
/// werden ignoriert. So können Doppelklicks oder ein Timeout-Race die Aktion
/// nicht überschreiben.
/// </remarks>
internal sealed class BreakReminderViewModel
{
    /// <summary>Sekunden bis zur automatischen Default-Aktion.</summary>
    public const int DefaultTimeoutSeconds = 30;

    // Einzige Quelle der Wahrheit ist ReminderTextKeys; hier nur als Alias gespiegelt.
    /// <summary>Lokalisierungs-Schlüssel des Titels.</summary>
    public const string TitleKey = ReminderTextKeys.Title;

    private bool _completed;

    /// <summary>
    /// Erzeugt das ViewModel für ein Pausenmodell.
    /// </summary>
    /// <param name="hintKey">Lokalisierungs-Schlüssel des Übungs-Hinweises.</param>
    public BreakReminderViewModel(string hintKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hintKey);
        HintKey = hintKey;
    }

    /// <summary>Wird ausgelöst, sobald eine Aktion feststeht (genau einmal).</summary>
    public event EventHandler<ReminderCompletedEventArgs>? Completed;

    /// <summary>Lokalisierungs-Schlüssel des angezeigten Übungs-Hinweises.</summary>
    public string HintKey { get; }

    /// <summary>Wahr, sobald eine Aktion gewählt wurde.</summary>
    public bool IsCompleted => _completed;

    /// <summary>Die gewählte Aktion, oder <c>null</c> solange offen.</summary>
    public ReminderResult? Result { get; private set; }

    /// <summary>Aktion "Pause starten".</summary>
    public void StartBreak() => Complete(ReminderResult.StartBreak);

    /// <summary>Aktion "5 Min später" (Snooze).</summary>
    public void Snooze() => Complete(ReminderResult.Snooze);

    /// <summary>Aktion "Überspringen".</summary>
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
