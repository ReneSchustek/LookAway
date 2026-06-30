using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Pausiert den Timer automatisch bei längerer Benutzer-Inaktivität und setzt
/// ihn bei wiederkehrender Aktivität fort. Reine Logik über
/// <see cref="IIdleDetector"/> und <see cref="ITimerService"/> — ohne Plattform-
/// oder UI-Abhängigkeit testbar.
/// </summary>
/// <remarks>
/// Ein selbst ausgelöster Idle-Pause wird gemerkt (<c>_pausedByIdle</c>), damit
/// eine vom Benutzer ausgelöste Pause nicht fälschlich fortgesetzt wird.
/// </remarks>
public sealed class IdleDetectionService
{
    /// <summary>Standard-Inaktivitätsschwelle (5 Minuten).</summary>
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromMinutes(5);

    private readonly IIdleDetector _idleDetector;
    private readonly ITimerService _timerService;
    private readonly ILogger<IdleDetectionService> _logger;
    private bool _pausedByIdle;
    private TimeSpan _awayWhilePaused;

    /// <summary>
    /// Erzeugt den Dienst mit Inaktivitäts-Quelle, Timer und Logger.
    /// </summary>
    /// <param name="idleDetector">Quelle der Inaktivitätsdauer.</param>
    /// <param name="timerService">Timer, der pausiert/fortgesetzt wird.</param>
    /// <param name="logger">Logger.</param>
    public IdleDetectionService(
        IIdleDetector idleDetector,
        ITimerService timerService,
        ILogger<IdleDetectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(idleDetector);
        ArgumentNullException.ThrowIfNull(timerService);
        ArgumentNullException.ThrowIfNull(logger);

        _idleDetector = idleDetector;
        _timerService = timerService;
        _logger = logger;
        Threshold = DefaultThreshold;
        IsEnabled = true;
    }

    /// <summary>Ist die automatische Idle-Pause aktiv?</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Inaktivitätsschwelle, ab der pausiert wird.</summary>
    public TimeSpan Threshold { get; set; }

    /// <summary>Wahr, solange der Timer wegen Inaktivität pausiert ist.</summary>
    public bool IsPausedByIdle => _pausedByIdle;

    /// <summary>
    /// Wertet die aktuelle Inaktivität aus und pausiert bzw. setzt den Timer
    /// fort. Pro Polling-Intervall einmal aufzurufen.
    /// </summary>
    public void Evaluate()
    {
        if (!IsEnabled)
        {
            ResumeIfPausedByIdle();
            return;
        }

        TimeSpan idleTime = _idleDetector.GetIdleTime();

        if (_pausedByIdle)
        {
            HandleWhilePausedByIdle(idleTime);
            return;
        }

        if (idleTime >= Threshold && IsRunning(_timerService.State))
        {
            _timerService.Pause();
            _pausedByIdle = true;
            _awayWhilePaused = idleTime;
            IdleDetectionLog.PausedDueToIdle(_logger, (int)idleTime.TotalSeconds);
        }
    }

    private void HandleWhilePausedByIdle(TimeSpan idleTime)
    {
        if (_timerService.State != TimerState.Paused)
        {
            // Zustand wurde anderweitig geändert — Anspruch aufgeben.
            _pausedByIdle = false;
            return;
        }

        if (idleTime < Threshold)
        {
            // War der Nutzer mindestens eine Pausenlänge weg, startet der Timer
            // eine frische Arbeitsphase (die Augen haben bereits geruht).
            _timerService.ResumeAfterAway(_awayWhilePaused);
            _pausedByIdle = false;
            IdleDetectionLog.ResumedAfterIdle(_logger);
            return;
        }

        // Noch immer inaktiv — längste beobachtete Abwesenheit fortschreiben.
        _awayWhilePaused = idleTime;
    }

    private void ResumeIfPausedByIdle()
    {
        if (_pausedByIdle && _timerService.State == TimerState.Paused)
        {
            _timerService.ResumeAfterAway(_awayWhilePaused);
        }
        _pausedByIdle = false;
    }

    private static bool IsRunning(TimerState state)
        => state is TimerState.Working or TimerState.OnBreak;
}

/// <summary>Source-generierte Logging-Methoden für den <see cref="IdleDetectionService"/>.</summary>
internal static partial class IdleDetectionLog
{
    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Information,
        Message = "Timer wegen Inaktivität pausiert ({IdleSeconds}s).")]
    public static partial void PausedDueToIdle(ILogger logger, int idleSeconds);

    [LoggerMessage(
        EventId = 3202,
        Level = LogLevel.Information,
        Message = "Timer nach wiederkehrender Aktivität fortgesetzt.")]
    public static partial void ResumedAfterIdle(ILogger logger);
}
