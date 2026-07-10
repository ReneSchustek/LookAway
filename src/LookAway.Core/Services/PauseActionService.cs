using LookAway.Core.Interfaces;

namespace LookAway.Core.Services;

/// <summary>
/// Führt beim Pausenbeginn und -ende die konfigurierten Pause-Aktionen aus:
/// Bildschirm dimmen und Medien pausieren. UI-frei und damit über
/// Fakes testbar; die Aktivierung steuert der Aufrufer über die Eigenschaften.
/// </summary>
public sealed class PauseActionService : IDisposable
{
    private readonly IScreenDimmer _screenDimmer;
    private readonly IMediaController _mediaController;

    // Begin und End werden vom Coordinator als „fire-and-forget"-Tasks gestartet und
    // können sich sonst überholen (End vor Begin → Bildschirm bliebe gedimmt). Das
    // Semaphore serialisiert Dimmen/Wiederherstellen strikt in Aufrufreihenfolge.
    private readonly SemaphoreSlim _sequence = new(1, 1);

    /// <summary>
    /// Erzeugt den Service.
    /// </summary>
    /// <param name="screenDimmer">Bildschirm-Dimmer.</param>
    /// <param name="mediaController">Medien-Steuerung.</param>
    public PauseActionService(IScreenDimmer screenDimmer, IMediaController mediaController)
    {
        ArgumentNullException.ThrowIfNull(screenDimmer);
        ArgumentNullException.ThrowIfNull(mediaController);
        _screenDimmer = screenDimmer;
        _mediaController = mediaController;
    }

    /// <summary>Soll der Bildschirm während der Pause gedimmt werden?</summary>
    public bool DimScreenEnabled { get; set; }

    /// <summary>Zielhelligkeit während der Pause in Prozent.</summary>
    public int DimBrightnessPercent { get; set; } = 30;

    /// <summary>Soll die Medienwiedergabe pausiert werden?</summary>
    public bool PauseMediaEnabled { get; set; }

    /// <summary>Sollen Medien nach der Pause wieder fortgesetzt werden?</summary>
    public bool ResumeMediaAfterBreak { get; set; } = true;

    /// <summary>Führt die Aktionen zum Pausenbeginn aus.</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task BeginBreakAsync(CancellationToken cancellationToken = default)
    {
        await _sequence.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DimScreenEnabled)
            {
                // DDC/CI-Aufrufe können blockieren — nicht auf dem UI-Thread ausführen.
                int target = DimBrightnessPercent;
                await Task.Run(() => _screenDimmer.DimTo(target), cancellationToken).ConfigureAwait(false);
            }

            if (PauseMediaEnabled)
            {
                await _mediaController.PauseAllAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _sequence.Release();
        }
    }

    /// <summary>Macht die Aktionen zum Pausenende rückgängig.</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task EndBreakAsync(CancellationToken cancellationToken = default)
    {
        await _sequence.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DimScreenEnabled)
            {
                await Task.Run(_screenDimmer.Restore, cancellationToken).ConfigureAwait(false);
            }

            if (PauseMediaEnabled && ResumeMediaAfterBreak)
            {
                await _mediaController.ResumeAllAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _sequence.Release();
        }
    }

    /// <summary>Gibt das interne Serialisierungs-Semaphore frei.</summary>
    public void Dispose() => _sequence.Dispose();
}
