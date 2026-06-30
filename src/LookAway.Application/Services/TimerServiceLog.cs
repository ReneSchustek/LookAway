using LookAway.Core.Enums;
using Microsoft.Extensions.Logging;

namespace LookAway.Application.Services;

/// <summary>
/// Source-generierte Logging-Methoden für den <see cref="TimerService"/>.
/// </summary>
internal static partial class TimerServiceLog
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Timer gestartet: Arbeit {WorkDuration}, Pause {BreakDuration}.")]
    public static partial void TimerStarted(ILogger logger, TimeSpan workDuration, TimeSpan breakDuration);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Timer gestoppt.")]
    public static partial void TimerStopped(ILogger logger);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Information,
        Message = "Pause fällig nach {WorkDuration}.")]
    public static partial void BreakDue(ILogger logger, TimeSpan workDuration);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Information,
        Message = "Pause beendet nach {BreakDuration}, Arbeit wird fortgesetzt.")]
    public static partial void BreakCompleted(ILogger logger, TimeSpan breakDuration);

    [LoggerMessage(
        EventId = 3020,
        Level = LogLevel.Information,
        Message = "Timer pausiert aus Zustand {PausedFromState} (durch System: {PausedBySystem}); Restzeit {Remaining}.")]
    public static partial void TimerPaused(ILogger logger, TimerState pausedFromState, bool pausedBySystem, TimeSpan remaining);

    [LoggerMessage(
        EventId = 3021,
        Level = LogLevel.Information,
        Message = "Timer fortgesetzt mit Zustand {ResumedState}.")]
    public static partial void TimerResumed(ILogger logger, TimerState resumedState);

    [LoggerMessage(
        EventId = 3022,
        Level = LogLevel.Information,
        Message = "Abwesenheit {AwayDuration} (aus {PausedFromState}) >= Pausenlänge — Arbeitsphase wird neu gestartet.")]
    public static partial void TimerRestartedAfterAway(ILogger logger, TimerState pausedFromState, TimeSpan awayDuration);

    [LoggerMessage(
        EventId = 3099,
        Level = LogLevel.Critical,
        Message = "Timer-Loop ist abgestürzt; Service wird beendet.")]
    public static partial void LoopCrashed(ILogger logger, Exception exception);
}
