using LookAway.Core.Interfaces;

namespace LookAway.Application.Services;

/// <summary>
/// Fuehrt beim Pausenbeginn und -ende die konfigurierten Pause-Aktionen aus
/// (BRIEF021): Bildschirm dimmen und Medien pausieren. UI-frei und damit ueber
/// Fakes testbar; die Aktivierung steuert der Aufrufer ueber die Eigenschaften.
/// </summary>
public sealed class PauseActionService
{
    private readonly IScreenDimmer _screenDimmer;
    private readonly IMediaController _mediaController;

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

    /// <summary>Soll der Bildschirm waehrend der Pause gedimmt werden?</summary>
    public bool DimScreenEnabled { get; set; }

    /// <summary>Zielhelligkeit waehrend der Pause in Prozent.</summary>
    public int DimBrightnessPercent { get; set; } = 30;

    /// <summary>Soll die Medienwiedergabe pausiert werden?</summary>
    public bool PauseMediaEnabled { get; set; }

    /// <summary>Sollen Medien nach der Pause wieder fortgesetzt werden?</summary>
    public bool ResumeMediaAfterBreak { get; set; } = true;

    /// <summary>Fuehrt die Aktionen zum Pausenbeginn aus.</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task BeginBreakAsync(CancellationToken cancellationToken = default)
    {
        if (DimScreenEnabled)
        {
            _screenDimmer.DimTo(DimBrightnessPercent);
        }

        if (PauseMediaEnabled)
        {
            await _mediaController.PauseAllAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Macht die Aktionen zum Pausenende rueckgaengig.</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task EndBreakAsync(CancellationToken cancellationToken = default)
    {
        if (DimScreenEnabled)
        {
            _screenDimmer.Restore();
        }

        if (PauseMediaEnabled && ResumeMediaAfterBreak)
        {
            await _mediaController.ResumeAllAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
