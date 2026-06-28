using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Media.Control;

namespace LookAway.Services;

/// <summary>
/// Pausiert und reaktiviert die System-Medienwiedergabe ueber die SMTC-API
/// (<see cref="GlobalSystemMediaTransportControlsSessionManager"/>).
/// Fehlt die API oder eine Session, bleibt der Aufruf wirkungslos.
/// </summary>
internal sealed class WindowsMediaController : IMediaController
{
    private readonly ILogger<WindowsMediaController> _logger;
    private readonly List<string> _pausedSources = new();

    /// <summary>Erzeugt die Medien-Steuerung.</summary>
    /// <param name="logger">Logger fuer SMTC-Fehler.</param>
    public WindowsMediaController(ILogger<WindowsMediaController> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "SMTC ist optional: jeder Fehler wird geloggt und ignoriert, damit die App nie abstuerzt.")]
    public async Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        _pausedSources.Clear();

        try
        {
            GlobalSystemMediaTransportControlsSessionManager manager =
                await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken).ConfigureAwait(false);

            foreach (GlobalSystemMediaTransportControlsSession session in manager.GetSessions())
            {
                GlobalSystemMediaTransportControlsSessionPlaybackInfo info = session.GetPlaybackInfo();
                if (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                    && await session.TryPauseAsync().AsTask(cancellationToken).ConfigureAwait(false))
                {
                    _pausedSources.Add(session.SourceAppUserModelId);
                }
            }
        }
        catch (Exception ex)
        {
            MediaControllerLog.PauseFailed(_logger, ex);
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "SMTC ist optional: jeder Fehler wird geloggt und ignoriert, damit die App nie abstuerzt.")]
    public async Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        if (_pausedSources.Count == 0)
        {
            return;
        }

        try
        {
            GlobalSystemMediaTransportControlsSessionManager manager =
                await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken).ConfigureAwait(false);

            foreach (GlobalSystemMediaTransportControlsSession session in manager.GetSessions())
            {
                if (_pausedSources.Contains(session.SourceAppUserModelId))
                {
                    _ = await session.TryPlayAsync().AsTask(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            MediaControllerLog.ResumeFailed(_logger, ex);
        }
        finally
        {
            _pausedSources.Clear();
        }
    }
}

/// <summary>
/// Source-generierte Logging-Methoden der Medien-Steuerung.
/// </summary>
internal static partial class MediaControllerLog
{
    [LoggerMessage(EventId = 1710, Level = LogLevel.Information, Message = "Medienwiedergabe konnte nicht pausiert werden (SMTC nicht verfuegbar?).")]
    public static partial void PauseFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1711, Level = LogLevel.Warning, Message = "Medienwiedergabe konnte nicht fortgesetzt werden.")]
    public static partial void ResumeFailed(ILogger logger, Exception exception);
}
