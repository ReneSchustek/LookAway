using LookAway.Application.Services;
using LookAway.Application.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Coordination;

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

    private BreakModel _model = BreakModel.ClassicPomodoro;
    private BreakInterval? _interval;
    private bool _soundEnabled;
    private SoundType _soundType = SoundType.Chime;
    private int _soundVolume;
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
        ILogger<BreakCoordinator> logger)
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
    }

    /// <summary>Manuelles Nicht-stören aktiv? (Nur für Tests/Diagnose.)</summary>
    public bool IsManualDndActive => _manualDnd;

    /// <summary>
    /// Übernimmt die Einstellungen: Modell/Intervall, Sound, Pause-Aktionen und
    /// Overlay-Optionen werden gesetzt, das Tray aktualisiert und der Timer mit dem
    /// effektiven Intervall (neu) gestartet.
    /// </summary>
    /// <param name="settings">Aktuelle Benutzerkonfiguration.</param>
    public void ApplySchedule(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        BreakInterval interval = BreakModelRegistry.GetEffective(settings.BreakModel, settings.CustomDurations);
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

        _tray.SetActiveModel(settings.BreakModel);
        _timer.Start(interval);
    }

    /// <summary>Verarbeitet ein Timer-Ereignis (Pause fällig / Pause beendet).</summary>
    /// <param name="timerEvent">Das Ereignis aus dem Timer-Strom.</param>
    public void HandleTimerEvent(TimerEvent timerEvent)
    {
        ArgumentNullException.ThrowIfNull(timerEvent);

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

    /// <summary>
    /// Zeigt die Erinnerung an (Hotkey „Pause starten" oder Nachholen nach DND).
    /// </summary>
    public void RequestReminder() => ShowReminder();

    /// <summary>Hotkey „Überspringen/Snooze": startet das aktuelle Intervall neu.</summary>
    public void SkipOrSnooze()
    {
        if (_interval is not null)
        {
            _timer.Start(_interval);
        }
    }

    /// <summary>Schaltet das manuelle Nicht-stören um und aktualisiert das Tray.</summary>
    public void ToggleManualDnd()
    {
        _manualDnd = !_manualDnd;
        _tray.SetDndActive(_manualDnd);
    }

    /// <summary>
    /// Aktualisiert die Tray-DND-Anzeige aus der Vollbild-Erkennung kombiniert mit
    /// dem manuellen Nicht-stören. Pro Erkennungs-Tick aufzurufen.
    /// </summary>
    /// <param name="fullscreenDndActive">Ist durch Vollbild aktuell DND aktiv?</param>
    public void UpdateDndIndicator(bool fullscreenDndActive)
        => _tray.SetDndActive(fullscreenDndActive || _manualDnd);

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
        _reminder.Show(_model, OnReminderResult);
    }

    private void OnReminderResult(ReminderResult result)
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
                    TimeSpan.FromMinutes(BreakReminderViewModel.SnoozeMinutes),
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
        _ = _pauseActions.EndBreakAsync();
        if (_interval is not null)
        {
            _timer.Start(_interval);
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

        BreakSession session = new(Guid.NewGuid(), _reminderShownAt, end, _model, outcome);
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
        Message = "Pausen-Historie konnte nicht geschrieben werden — Statistik bleibt unveraendert.")]
    public static partial void HistoryWriteFailed(ILogger logger, Exception exception);
}
