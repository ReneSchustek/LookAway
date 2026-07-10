using LookAway.Core.Enums;
using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Zustand und Aktionslogik der Pause-Erinnerung — UI-frei und damit ohne WinUI
/// testbar. Ist ein automatischer Pausenstart konfiguriert, zählt das ViewModel die
/// verbleibenden Sekunden herunter (die View taktet <see cref="Tick"/> im
/// Sekundentakt) und startet die Pause selbsttätig, sobald der Zähler 0 erreicht.
/// </summary>
/// <remarks>
/// Die erste gewählte Aktion gewinnt; weitere Aufrufe (auch ein ablaufender
/// Countdown) werden ignoriert. So können Doppelklicks oder ein Timeout-Race die
/// Aktion nicht überschreiben.
/// </remarks>
internal sealed class BreakReminderViewModel
{
    // Einzige Quelle der Wahrheit ist ReminderTextKeys; hier nur als Alias gespiegelt.
    /// <summary>Lokalisierungs-Schlüssel des Titels.</summary>
    public const string TitleKey = ReminderTextKeys.Title;

    private bool _completed;
    private int _remainingSeconds;

    /// <summary>
    /// Erzeugt das ViewModel für ein Pausenmodell.
    /// </summary>
    /// <param name="hintKey">Lokalisierungs-Schlüssel des Übungs-Hinweises.</param>
    /// <param name="autoStartSeconds">
    /// Verbleibende Sekunden bis zum automatischen Pausenstart, oder <c>null</c>, wenn
    /// die Erinnerung bis zu einer Benutzeraktion offen bleibt (kein Countdown).
    /// </param>
    public BreakReminderViewModel(string hintKey, int? autoStartSeconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hintKey);
        HintKey = hintKey;
        AutoStartsAutomatically = autoStartSeconds is > 0;
        _remainingSeconds = autoStartSeconds is > 0 ? autoStartSeconds.Value : 0;
    }

    /// <summary>Wird ausgelöst, sobald eine Aktion feststeht (genau einmal).</summary>
    public event EventHandler<ReminderCompletedEventArgs>? Completed;

    /// <summary>Lokalisierungs-Schlüssel des angezeigten Übungs-Hinweises.</summary>
    public string HintKey { get; }

    /// <summary>Wahr, sobald eine Aktion gewählt wurde.</summary>
    public bool IsCompleted => _completed;

    /// <summary>Startet die Pause nach Ablauf des Countdowns von selbst?</summary>
    public bool AutoStartsAutomatically { get; }

    /// <summary>Verbleibende Sekunden bis zum automatischen Pausenstart.</summary>
    public int RemainingSeconds => _remainingSeconds;

    /// <summary>Die gewählte Aktion, oder <c>null</c> solange offen.</summary>
    public ReminderResult? Result { get; private set; }

    /// <summary>Aktion "Pause starten".</summary>
    public void StartBreak() => Complete(ReminderResult.StartBreak);

    /// <summary>Aktion "5 Min später" (Snooze).</summary>
    public void Snooze() => Complete(ReminderResult.Snooze);

    /// <summary>Aktion "Überspringen".</summary>
    public void Skip() => Complete(ReminderResult.Skip);

    /// <summary>
    /// Zählt den Countdown eine Sekunde herunter (von der View im Sekundentakt
    /// aufgerufen). Erreicht der Zähler 0, startet die Pause automatisch. Ohne
    /// konfigurierten Auto-Start oder nach einer Benutzeraktion wirkungslos.
    /// </summary>
    public void Tick()
    {
        if (_completed || !AutoStartsAutomatically)
        {
            return;
        }

        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
        }

        if (_remainingSeconds <= 0)
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
