using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Core.Services;

/// <summary>
/// Orchestriert den Pausen-Ablauf der Anwendung: reagiert auf Timer-Ereignisse,
/// zeigt Erinnerung und Overlay, führt die Pause-Aktionen aus, schreibt die
/// Historie und steuert die Tray-Anzeige. UI- und plattformfrei (nur über
/// Core-Interfaces und Presenter-Abstraktionen) und damit vollständig testbar;
/// die App-Schicht liefert nur die konkreten Presenter, das Tray und die
/// Ereignisschleifen.
/// </summary>
public sealed class BreakCoordinator
{
    private readonly ITimerService _timer;
    private readonly IReminderPresenter _reminder;
    private readonly IBreakOverlayPresenter _overlay;
    private readonly PauseActionService _pauseActions;
    private readonly ISoundService _sound;
    private readonly IClock _clock;
    private readonly IBreakHistoryRepository _history;
    private readonly ITrayController _tray;
    private readonly FullscreenDetectionService _fullscreen;
    private readonly ILogger<BreakCoordinator> _logger;

    // Der Coordinator wird aus zwei Threads bedient: Timer-Ereignisse, Hotkeys und
    // Presenter-Callbacks laufen auf dem UI-Thread, die Erkennungs-Schleife
    // (UpdateDndIndicator, RequestReminder) auf einem Hintergrund-Thread. Der Lock
    // hält den veränderlichen Zustand konsistent. Er ist reentrant, sodass
    // HandleTimerEvent gefahrlos ShowReminder aufrufen darf.
    private readonly Lock _gate = new();

    private BreakModel _model = BreakModel.ClassicPomodoro;
    // Optional: Nur beim aufgabenbasierten Modell hängt eine Pause an einer Aufgabe.
    private readonly CurrentWorkTaskTracker? _workTasks;

    private BreakInterval? _interval;
    private bool _soundEnabled;
    private SoundType _soundType = SoundType.Chime;
    private int _soundVolume;
    // Verzögerung bis zum automatischen Pausenstart, oder null wenn deaktiviert
    // (dann bleibt die Erinnerung offen, bis der Benutzer eine Aktion wählt).
    private TimeSpan? _autoStartBreakAfter = TimeSpan.FromSeconds(15);
    private string _overlayColor = HexColor.Default;
    private bool _darkenAllScreens = true;
    private bool _manualDnd;
    private DateTimeOffset _reminderShownAt;

    /// <summary>Erzeugt den Coordinator mit seinen Abhängigkeiten.</summary>
    public BreakCoordinator(
        ITimerService timer,
        IReminderPresenter reminder,
        IBreakOverlayPresenter overlay,
        PauseActionService pauseActions,
        ISoundService sound,
        IClock clock,
        IBreakHistoryRepository history,
        ITrayController tray,
        FullscreenDetectionService fullscreen,
        ILogger<BreakCoordinator> logger,
        CurrentWorkTaskTracker? workTasks = null)
    {
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(reminder);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(pauseActions);
        ArgumentNullException.ThrowIfNull(sound);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(fullscreen);
        ArgumentNullException.ThrowIfNull(logger);

        _timer = timer;
        _reminder = reminder;
        _overlay = overlay;
        _pauseActions = pauseActions;
        _sound = sound;
        _clock = clock;
        _history = history;
        _tray = tray;
        _fullscreen = fullscreen;
        _logger = logger;
        _workTasks = workTasks;
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

    private void ApplyScheduleLocked(Settings settings, BreakInterval interval, TimeSpan? resumeWorkRemaining)
    {
        // Den laufenden Countdown nur dann (neu) starten, wenn sich das effektive
        // Intervall (Arbeits-/Pausendauer) tatsächlich geändert hat oder der Timer
        // noch nicht läuft. So setzt das bloße Speichern unveränderter Intervalle —
        // etwa von Sprache, Ton, Overlay-Farbe oder Update-Häufigkeit — den Timer
        // NICHT zurück. (Die Rücksetzung nach Standby/Bildschirm-Aus bleibt davon
        // unberührt; sie läuft über den Power-/Idle-Pfad des TimerService.)
        bool intervalUnchanged = _interval is not null
            && _timer.State != TimerState.Idle
            && _interval.WorkDuration == interval.WorkDuration
            && _interval.BreakDuration == interval.BreakDuration;

        _model = settings.BreakModel;
        _interval = interval;

        _soundEnabled = settings.SoundEnabled;
        _soundType = settings.ReminderSound;
        _soundVolume = settings.SoundVolumePercent;

        _pauseActions.DimScreenEnabled = settings.DimScreenDuringBreak;
        _pauseActions.DimBrightnessPercent = settings.DimBrightnessPercent;
        _pauseActions.PauseMediaEnabled = settings.PauseMediaDuringBreak;
        _pauseActions.ResumeMediaAfterBreak = settings.ResumeMediaAfterBreak;

        _overlayColor = settings.BreakOverlayColor;
        _darkenAllScreens = settings.DarkenAllScreens;

        _autoStartBreakAfter = settings.AutoStartBreakEnabled
            ? TimeSpan.FromSeconds(settings.AutoStartBreakSeconds)
            : null;

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

    /// <summary>Hotkey „Überspringen/Snooze": startet das aktuelle Intervall neu.</summary>
    public void SkipOrSnooze()
    {
        lock (_gate)
        {
            if (_interval is not null)
            {
                _timer.Start(_interval);
            }
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

    // Erwartet den gehaltenen _gate-Lock.
    private void ShowReminder()
    {
        if (_reminder.IsReminderOpen || _manualDnd)
        {
            return;
        }

        if (_soundEnabled)
        {
            _sound.Play(_soundType, _soundVolume);
        }

        _reminderShownAt = _clock.UtcNow;
        _reminder.Show(_model, _autoStartBreakAfter, OnReminderResult);
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
        RecordSession(result);

        if (_interval is null)
        {
            return;
        }

        switch (result)
        {
            case ReminderResult.StartBreak:
                // Pause-Aktionen starten und den Bildschirm mit dem Overlay verdecken;
                // ab hier ist das Overlay maßgeblich für das Pausenende.
                _ = _pauseActions.BeginBreakAsync();
                _overlay.Show(_model, _interval.BreakDuration, _overlayColor, _darkenAllScreens, OnOverlayEnded);
                break;
            case ReminderResult.Snooze:
                _timer.Start(BreakInterval.Create(
                    BreakDefaults.SnoozeWorkDuration,
                    _interval.BreakDuration));
                break;
            case ReminderResult.Skip:
                _timer.Start(_interval);
                break;
            default:
                break;
        }
    }

    private void OnOverlayEnded(BreakEndReason reason)
    {
        // Egal ob abgelaufen oder per ESC beendet: Pause-Aktionen zurücknehmen und
        // eine frische Arbeitsphase starten.
        _ = reason;
        lock (_gate)
        {
            _ = _pauseActions.EndBreakAsync();
            if (_interval is not null)
            {
                _timer.Start(_interval);
            }
        }
    }

    private void RecordSession(ReminderResult result)
    {
        BreakOutcome outcome = result switch
        {
            ReminderResult.StartBreak => BreakOutcome.Taken,
            ReminderResult.Snooze => BreakOutcome.Snoozed,
            ReminderResult.Skip => BreakOutcome.Skipped,
            _ => BreakOutcome.Taken,
        };

        DateTimeOffset end = outcome == BreakOutcome.Taken && _interval is not null
            ? _reminderShownAt + _interval.BreakDuration
            : _reminderShownAt;

        // Die Aufgabe wird nur beim aufgabenbasierten Modell vermerkt: Bei den übrigen
        // entsteht die Pause aus der Uhr und nicht aus dem, was man gerade tut.
        Guid? taskId = _model == BreakModel.TaskBased ? _workTasks?.CurrentTaskId : null;

        BreakSession session = new(Guid.NewGuid(), _reminderShownAt, end, _model, outcome, taskId);
        _ = AppendSessionAsync(session);
    }

    private async Task AppendSessionAsync(BreakSession session)
    {
        try
        {
            await _history.AppendAsync(session).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            BreakCoordinatorLog.HistoryWriteFailed(_logger, ex);
        }
        catch (IOException ex)
        {
            BreakCoordinatorLog.HistoryWriteFailed(_logger, ex);
        }
    }
}

/// <summary>Source-generierte Logging-Methoden des Coordinators.</summary>
internal static partial class BreakCoordinatorLog
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Warning,
        Message = "Pausen-Historie konnte nicht geschrieben werden — Statistik bleibt unverändert.")]
    public static partial void HistoryWriteFailed(ILogger logger, Exception exception);
}
