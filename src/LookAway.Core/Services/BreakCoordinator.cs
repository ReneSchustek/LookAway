using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Services;

/// <summary>
/// Orchestriert den Pausen-Ablauf der Anwendung: reagiert auf Timer-Ereignisse,
/// zeigt Erinnerung und Overlay, führt die Pause-Aktionen aus, schreibt die
/// Historie und steuert die Tray-Anzeige. UI- und plattformfrei (nur über
/// Core-Interfaces und Presenter-Abstraktionen) und damit vollständig testbar;
/// die App-Schicht liefert nur die konkreten Presenter, das Tray und die
/// Ereignisschleifen.
/// </summary>
// Über der Grenze: Erinnerung, Pause, Nicht-stören und Zeitplan teilen einen Zustand hinter einer
// einzigen Sperre zwischen UI-Thread und Erkennungsschleife. Historie, Vorlieben und Anzeigen sind
// schon ausgelagert; eine weitere Teilung müsste die Sperre über Klassen verteilen.
public sealed class BreakCoordinator : IBreakSignalSink
{
    private readonly ITimerService _timer;
    private readonly IReminderPresenter _reminder;
    private readonly IBreakOverlayPresenter _overlay;
    private readonly ITrayController _tray;
    private readonly PauseActionService _pauseActions;
    private readonly ISoundService _sound;
    private readonly IClock _clock;
    private readonly BreakHistoryRecorder _recorder;
    private readonly FullscreenDetectionService _fullscreen;

    // Der Coordinator wird aus zwei Threads bedient: Timer-Ereignisse, Hotkeys und
    // Presenter-Callbacks laufen auf dem UI-Thread, die Erkennungs-Schleife
    // (UpdateDndIndicator, RequestReminder) auf einem Hintergrund-Thread. Der Lock
    // hält den veränderlichen Zustand konsistent. Er ist reentrant, sodass
    // HandleTimerEvent gefahrlos ShowReminder aufrufen darf.
    private readonly Lock _gate = new();

    private BreakModel _model = Settings.DefaultBreakModel;
    private BreakInterval? _interval;
    private BreakPreferences _preferences = BreakPreferences.Default;
    private bool _manualDnd;
    private DateTimeOffset _reminderShownAt;

    // Beginn der laufenden Pause, solange das Overlay offen ist. Aufgezeichnet wird die Pause
    // erst an ihrem Ende, damit die Historie die tatsächliche Dauer trägt und nicht die
    // geplante: Wer per Esc nach einer Minute aufhört, hat eine Minute Pause gemacht.
    private DateTimeOffset? _breakStartedAt;

    /// <summary>Erzeugt den Coordinator mit seinen Abhängigkeiten.</summary>
    /// <param name="timer">Timer, dessen Ereignisse den Ablauf treiben.</param>
    /// <param name="presenters">Erinnerung, Overlay und Infobereich.</param>
    /// <param name="pauseActions">Abdunkeln und Medien während der Pause.</param>
    /// <param name="sound">Erinnerungston.</param>
    /// <param name="clock">Uhr für Beginn und Ende der aufgezeichneten Pausen.</param>
    /// <param name="recorder">Schreibt Pausen in die Historie.</param>
    /// <param name="fullscreen">Entscheidet, ob gerade erinnert werden darf.</param>
    public BreakCoordinator(
        ITimerService timer,
        BreakPresenters presenters,
        PauseActionService pauseActions,
        ISoundService sound,
        IClock clock,
        BreakHistoryRecorder recorder,
        FullscreenDetectionService fullscreen)
    {
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(presenters);
        ArgumentNullException.ThrowIfNull(pauseActions);
        ArgumentNullException.ThrowIfNull(sound);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(fullscreen);

        _timer = timer;
        (_reminder, _overlay, _tray) = presenters;
        _pauseActions = pauseActions;
        _sound = sound;
        _clock = clock;
        _recorder = recorder;
        _fullscreen = fullscreen;
    }

    /// <summary>Manuelles Nicht-stören aktiv? (Nur für Tests/Diagnose.)</summary>
    public bool IsManualDndActive
    {
        get
        {
            lock (_gate)
            {
                return _manualDnd;
            }
        }
    }

    /// <summary>Aktuell aktives Pausenmodell.</summary>
    public BreakModel ActiveModel
    {
        get
        {
            lock (_gate)
            {
                return _model;
            }
        }
    }

    /// <summary>Läuft gerade eine Arbeitsphase (für die Momentaufnahme beim Beenden)?</summary>
    public bool IsWorking => _timer.State == TimerState.Working;

    /// <summary>Verbleibende Arbeitszeit der laufenden Phase.</summary>
    public TimeSpan WorkRemaining => _timer.Remaining;

    /// <summary>
    /// Übernimmt die Einstellungen: Modell/Intervall, Sound, Pause-Aktionen und
    /// Overlay-Optionen werden gesetzt und das Tray aktualisiert. Der Timer wird nur
    /// dann (neu) gestartet, wenn sich das effektive Intervall geändert hat oder er
    /// noch nicht läuft — bloßes Speichern unveränderter Intervalle setzt den
    /// laufenden Countdown also nicht zurück.
    /// </summary>
    /// <param name="settings">Aktuelle Benutzerkonfiguration.</param>
    /// <param name="resumeWorkRemaining">
    /// Wenn gesetzt und der Timer noch nicht läuft: verbleibende Arbeitszeit, mit der
    /// der Countdown fortgesetzt wird (nahtloser Neustart in derselben Sitzung, z. B.
    /// nach einer Aktualisierung), statt auf die volle Arbeitsdauer zurückzusetzen.
    /// </param>
    public void ApplySchedule(Settings settings, TimeSpan? resumeWorkRemaining = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        BreakInterval interval = BreakModelRegistry.GetEffective(settings.BreakModel, settings.CustomDurations);

        lock (_gate)
        {
            ApplyScheduleLocked(settings, interval, resumeWorkRemaining);
        }
    }

    /// <summary>Verarbeitet ein Timer-Ereignis (Pause fällig / Pause beendet).</summary>
    /// <param name="timerEvent">Das Ereignis aus dem Timer-Strom.</param>
    public void HandleTimerEvent(TimerEvent timerEvent)
    {
        ArgumentNullException.ThrowIfNull(timerEvent);

        lock (_gate)
        {
            switch (timerEvent)
            {
                case BreakDueEvent:
                    // Vollbild-/DND-Erkennung entscheidet, ob jetzt erinnert werden darf.
                    if (_fullscreen.TryShowReminder())
                    {
                        ShowReminder();
                    }

                    break;
                case BreakCompletedEvent:
                    // Ist das Overlay offen, ist es maßgeblich für das Pausenende
                    // (siehe OnOverlayEnded); sonst (ignorierte Erinnerung) hier aufräumen.
                    if (!_overlay.IsOverlayOpen)
                    {
                        _ = _pauseActions.EndBreakAsync();
                    }

                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Zeigt die Erinnerung an (Hotkey „Pause starten" oder Nachholen nach DND).
    /// </summary>
    public void RequestReminder()
    {
        lock (_gate)
        {
            ShowReminder();
        }
    }

    /// <summary>
    /// Hotkey „Überspringen/Snooze": beendet eine laufende Pause, sonst startet er das
    /// aktuelle Intervall neu.
    /// </summary>
    /// <remarks>
    /// Während der Pause deckt das Overlay jeden Monitor ab und liegt über allen
    /// anderen Fenstern — es lässt sich weder wegklicken noch in den Hintergrund
    /// schieben. Damit ESC nicht der einzige Ausweg bleibt, ist der Hotkey hier der
    /// Notausstieg; er nimmt die Pause-Aktionen genauso zurück wie das reguläre Ende.
    /// </remarks>
    public void SkipOrSnooze()
    {
        lock (_gate)
        {
            if (_overlay.IsOverlayOpen)
            {
                _overlay.Close();
                EndBreakLocked();
                return;
            }

            RestartIntervalLocked();
        }
    }

    /// <summary>Schaltet das manuelle Nicht-stören um und aktualisiert das Tray.</summary>
    public void ToggleManualDnd()
    {
        lock (_gate)
        {
            _manualDnd = !_manualDnd;
            _tray.SetDndActive(_manualDnd);
        }
    }

    /// <summary>
    /// Aktualisiert die Tray-DND-Anzeige aus der Vollbild-Erkennung kombiniert mit
    /// dem manuellen Nicht-stören. Pro Erkennungs-Tick aufzurufen.
    /// </summary>
    /// <param name="fullscreenDndActive">Ist durch Vollbild aktuell DND aktiv?</param>
    public void UpdateDndIndicator(bool fullscreenDndActive)
    {
        lock (_gate)
        {
            _tray.SetDndActive(fullscreenDndActive || _manualDnd);
        }
    }

    private void ApplyScheduleLocked(Settings settings, BreakInterval interval, TimeSpan? resumeWorkRemaining)
    {
        // Wer nur Sprache, Ton oder Overlay-Farbe speichert, verliert seinen Countdown
        // nicht. Standby und Bildschirm-Aus setzen ihn trotzdem zurück; das läuft über
        // den Power- und Idle-Pfad des TimerService, nicht über diese Methode.
        bool intervalUnchanged = _interval is not null
            && _timer.State != TimerState.Idle
            && _interval.WorkDuration == interval.WorkDuration
            && _interval.BreakDuration == interval.BreakDuration;

        _model = settings.BreakModel;
        _interval = interval;
        _preferences = BreakPreferences.From(settings);
        ConfigurePauseActions(settings);
        _tray.SetActiveModel(settings.BreakModel);

        if (resumeWorkRemaining is { } remaining && _timer.State == TimerState.Idle)
        {
            // Nahtloser Neustart innerhalb derselben Windows-Sitzung (z. B. nach einer
            // Aktualisierung): mit der gemerkten Restzeit fortsetzen statt auf die volle
            // Arbeitsdauer zurückzusetzen.
            _timer.RestoreWorking(interval, remaining);
        }
        else if (!intervalUnchanged)
        {
            _timer.Start(interval);
        }
    }

    private void ConfigurePauseActions(Settings settings)
    {
        _pauseActions.DimScreenEnabled = settings.DimScreenDuringBreak;
        _pauseActions.DimBrightnessPercent = settings.DimBrightnessPercent;
        _pauseActions.PauseMediaEnabled = settings.PauseMediaDuringBreak;
        _pauseActions.ResumeMediaAfterBreak = settings.ResumeMediaAfterBreak;
    }

    // Erwartet den gehaltenen _gate-Lock.
    private void ShowReminder()
    {
        if (_reminder.IsReminderOpen || _manualDnd)
        {
            return;
        }

        if (_preferences.SoundEnabled)
        {
            _sound.Play(_preferences.Sound, _preferences.SoundVolume);
        }

        _reminderShownAt = _clock.UtcNow;
        _reminder.Show(_model, _preferences.AutoStartBreakAfter, OnReminderResult);
    }

    private void OnReminderResult(ReminderResult result)
    {
        lock (_gate)
        {
            OnReminderResultLocked(result);
        }
    }

    private void OnReminderResultLocked(ReminderResult result)
    {
        switch (result)
        {
            case ReminderResult.StartBreak when _interval is not null:
                BeginBreakLocked(_interval);
                break;
            case ReminderResult.Snooze:
                _recorder.Record(_model, BreakOutcome.Snoozed, _reminderShownAt, _reminderShownAt);
                if (_interval is not null)
                {
                    _timer.Start(BreakInterval.Create(BreakDefaults.SnoozeWorkDuration, _interval.BreakDuration));
                }

                break;
            case ReminderResult.Skip:
                _recorder.Record(_model, BreakOutcome.Skipped, _reminderShownAt, _reminderShownAt);
                RestartIntervalLocked();
                break;
            default:
                // Ohne Intervall läuft noch kein Zeitplan; eine Pause, die nie begann, wird
                // nicht aufgezeichnet.
                break;
        }
    }

    // Pause-Aktionen starten und den Bildschirm mit dem Overlay verdecken; ab hier ist das
    // Overlay maßgeblich für das Pausenende und die Aufzeichnung.
    private void BeginBreakLocked(BreakInterval interval)
    {
        _breakStartedAt = _clock.UtcNow;
        _ = _pauseActions.BeginBreakAsync();
        _overlay.Show(_model, interval.BreakDuration, _preferences.OverlayBackdrop, _preferences.DarkenAllScreens, OnOverlayEnded);
    }

    private void OnOverlayEnded(BreakEndReason reason)
    {
        // Egal ob abgelaufen oder per ESC beendet: derselbe Abschluss.
        _ = reason;
        lock (_gate)
        {
            EndBreakLocked();
        }
    }

    // Erwartet den gehaltenen _gate-Lock. Nimmt die Pause-Aktionen zurück und beginnt
    // eine frische Arbeitsphase — unabhängig davon, wodurch die Pause endete. Verschobene und
    // übersprungene Erinnerungen haben keine Dauer; eine angenommene Pause reicht vom
    // Pausenbeginn bis zu ihrem tatsächlichen Ende. Endet das Programm mitten in der Pause,
    // fehlt sie in der Historie: Ein halber Eintrag mit geschätztem Ende wäre die schlechtere Wahl.
    private void EndBreakLocked()
    {
        if (_breakStartedAt is { } startedAt)
        {
            _breakStartedAt = null;
            _recorder.Record(_model, BreakOutcome.Taken, startedAt, _clock.UtcNow);
        }

        _ = _pauseActions.EndBreakAsync();
        RestartIntervalLocked();
    }

    // Erwartet den gehaltenen _gate-Lock. Ohne Intervall läuft noch kein Zeitplan.
    private void RestartIntervalLocked()
    {
        if (_interval is not null)
        {
            _timer.Start(_interval);
        }
    }
}
