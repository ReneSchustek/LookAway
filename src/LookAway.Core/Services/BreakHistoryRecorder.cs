using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Core.Services;

/// <summary>
/// Schreibt Pausen in die Historie, beim aufgabenbasierten Modell mit der Aufgabe, an der gerade
/// gearbeitet wurde. Ein Schreibfehler wird protokolliert und hält den Pausen-Ablauf nicht auf.
/// </summary>
public sealed class BreakHistoryRecorder
{
    private readonly IBreakHistoryRepository _history;
    private readonly ILogger<BreakHistoryRecorder> _logger;
    private readonly CurrentWorkTaskTracker? _workTasks;

    /// <summary>Erzeugt den Schreiber über seiner Ablage.</summary>
    /// <param name="history">Ablage der Pausen-Historie.</param>
    /// <param name="logger">Protokolliert gescheiterte Schreibvorgänge.</param>
    /// <param name="workTasks">Liefert die laufende Aufgabe; ohne ihn bleibt die Aufgabe leer.</param>
    public BreakHistoryRecorder(IBreakHistoryRepository history, ILogger<BreakHistoryRecorder> logger, CurrentWorkTaskTracker? workTasks = null)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(logger);
        _history = history;
        _logger = logger;
        _workTasks = workTasks;
    }

    /// <summary>Hängt eine Pause an die Historie an, ohne auf das Schreiben zu warten.</summary>
    /// <param name="model">Pausenmodell, unter dem die Pause stattfand.</param>
    /// <param name="outcome">Angenommen, verschoben oder übersprungen.</param>
    /// <param name="start">Beginn; bei verschobenen und übersprungenen Erinnerungen ihr Zeitpunkt.</param>
    /// <param name="end">Tatsächliches Ende; ohne Pause gleich dem Beginn.</param>
    public void Record(BreakModel model, BreakOutcome outcome, DateTimeOffset start, DateTimeOffset end)
    {
        // Die Aufgabe wird nur beim aufgabenbasierten Modell vermerkt: Bei den übrigen
        // entsteht die Pause aus der Uhr und nicht aus dem, was man gerade tut.
        Guid? taskId = model == BreakModel.TaskBased ? _workTasks?.CurrentTaskId : null;

        BreakSession session = new(Guid.NewGuid(), start, end, model, outcome, taskId);
        _ = AppendAsync(session);
    }

    private async Task AppendAsync(BreakSession session)
    {
        try
        {
            await _history.AppendAsync(session).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            BreakHistoryRecorderLog.HistoryWriteFailed(_logger, ex);
        }
    }
}

/// <summary>Source-generierte Logging-Methoden des Historien-Schreibers.</summary>
internal static partial class BreakHistoryRecorderLog
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Warning,
        Message = "Pausen-Historie konnte nicht geschrieben werden — Statistik bleibt unverändert.")]
    public static partial void HistoryWriteFailed(ILogger logger, Exception exception);
}
