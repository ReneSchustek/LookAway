using LookAway.Core.Entities;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;

namespace LookAway.App.Services;

/// <summary>
/// Betreibt die beiden Hintergrund-Schleifen der App: die periodische Idle-/
/// Vollbild-Erkennung und den Verbrauch des Timer-Ereignisstroms. Beide speisen
/// den Pausen-Ablauf über <see cref="IBreakSignalSink"/>. Die Schleifen liegen hier und nicht in der
/// App-Klasse, damit diese nur verdrahtet.
/// </summary>
internal sealed class DetectionLoopHost : IDisposable
{
    // Die Idle-Schwelle zählt in Minuten, und ein Vollbildwechsel ist nach fünf Sekunden
    // erkannt. Häufiger zu fragen brächte nichts außer zusätzlichen Win32-Aufrufen.
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

    private readonly IdleDetectionService _idle;
    private readonly FullscreenDetectionService _fullscreen;
    private readonly ITimerService _timer;
    private readonly IBreakSignalSink _coordinator;
    private readonly TimeSpan _pollInterval;

    private CancellationTokenSource? _cts;

    /// <summary>Erzeugt den Host mit den beteiligten Diensten.</summary>
    /// <param name="idle">Inaktivitäts-Erkennung.</param>
    /// <param name="fullscreen">Vollbild-/DND-Erkennung.</param>
    /// <param name="timer">Timer-Dienst (Ereignisquelle).</param>
    /// <param name="coordinator">Pausen-Ablauf, der die Ereignisse verarbeitet.</param>
    /// <param name="pollInterval">Abstand der Erkennungsabfragen; <c>null</c> für fünf Sekunden.</param>
    public DetectionLoopHost(
        IdleDetectionService idle,
        FullscreenDetectionService fullscreen,
        ITimerService timer,
        IBreakSignalSink coordinator,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(idle);
        ArgumentNullException.ThrowIfNull(fullscreen);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(coordinator);

        _idle = idle;
        _fullscreen = fullscreen;
        _timer = timer;
        _coordinator = coordinator;
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    /// <summary>
    /// Übernimmt die Erkennungs-Einstellungen (Idle-Schwelle, Vollbild-Unterdrückung).
    /// Wird beim Start und bei jeder Einstellungsänderung aufgerufen.
    /// </summary>
    /// <param name="settings">Aktuelle Konfiguration.</param>
    public void ApplySettings(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _idle.IsEnabled = settings.PauseOnIdle;
        _idle.Threshold = TimeSpan.FromMinutes(settings.IdleThresholdMinutes);
        _fullscreen.IsEnabled = settings.SuppressOnFullscreen;
    }

    /// <summary>Startet die Erkennungs- und die Timer-Ereignis-Schleife.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = RunDetectionLoopAsync(_cts.Token);
        _ = ConsumeTimerEventsAsync(_cts.Token);
    }

    private async Task RunDetectionLoopAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(_pollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                _idle.Evaluate();
                bool surfaceMissedReminder = _fullscreen.Evaluate();
                _coordinator.UpdateDndIndicator(_fullscreen.IsDndActive);

                if (surfaceMissedReminder)
                {
                    // DND wurde beendet: verpasste Erinnerung nachholen.
                    _coordinator.RequestReminder();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartetes Ende beim Shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Tray/Services beim Shutdown bereits freigegeben — unkritisch.
        }
    }

    private async Task ConsumeTimerEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TimerEvent timerEvent in _timer.Events.WithCancellation(cancellationToken))
            {
                _coordinator.HandleTimerEvent(timerEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartetes Ende beim Shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Container/Services beim Shutdown bereits freigegeben — unkritisch.
        }
    }

    /// <summary>Beendet die Schleifen und gibt das Abbruch-Token frei.</summary>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
