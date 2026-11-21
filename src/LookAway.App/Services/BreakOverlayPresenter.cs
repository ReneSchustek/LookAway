using LookAway.App.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Implementierung von <see cref="IBreakOverlayPresenter"/>: legt für eine Pause den gemeinsamen
/// Zustand an, lässt die Fensterseite je Monitor ein Overlay öffnen und taktet den gemeinsamen
/// Countdown. Auf Wunsch wird jeder angeschlossene Monitor mit einem eigenen Overlay abgedeckt;
/// der Countdown bleibt über alle Fenster synchron. Es ist höchstens ein Satz Overlays offen; ein
/// zweites Show verfällt. Ein gewähltes Bild wird vorher probeweise gelesen; taugt es nicht, zeigt
/// das Overlay die Farbe.
/// </summary>
internal sealed class BreakOverlayPresenter : IBreakOverlayPresenter
{
    private readonly IBreakOverlayUi _ui;
    private readonly IOverlayImageInspector _imageInspector;
    private readonly ILogger<BreakOverlayPresenter> _logger;
    // Wird auf dem UI-Thread gesetzt, aber vom Timer-Consumer-Thread gelesen
    // (BreakCompletedEvent-Gate) — daher volatile für korrekte Sichtbarkeit.
    private volatile bool _isOverlayOpen;

    // Zählt jedes Show und Close. Das Bild wird außerhalb des UI-Threads gelesen; wird die Pause
    // inzwischen geschlossen oder neu gezeigt, verfällt der veraltete Fensteraufbau daran.
    private int _generation;

    /// <summary>
    /// Erzeugt den Presenter über seiner Fensterseite.
    /// </summary>
    /// <param name="ui">Öffnet, taktet und schließt die Overlay-Fenster.</param>
    /// <param name="imageInspector">Liest ein gewähltes Overlay-Bild probeweise.</param>
    /// <param name="logger">Protokolliert fehlgeschlagene Fenster-Aufbauten.</param>
    public BreakOverlayPresenter(IBreakOverlayUi ui, IOverlayImageInspector imageInspector, ILogger<BreakOverlayPresenter> logger)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(imageInspector);
        ArgumentNullException.ThrowIfNull(logger);
        _ui = ui;
        _imageInspector = imageInspector;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsOverlayOpen => _isOverlayOpen;

    /// <inheritdoc />
    public void Show(BreakModel model, TimeSpan breakDuration, OverlayBackdrop backdrop, bool allScreens, Action<BreakEndReason> onEnded)
    {
        ArgumentNullException.ThrowIfNull(backdrop);
        ArgumentNullException.ThrowIfNull(onEnded);

        if (_isOverlayOpen || breakDuration <= TimeSpan.Zero)
        {
            return;
        }

        _isOverlayOpen = true;
        OverlayRequest request = new(Interlocked.Increment(ref _generation), model, breakDuration, allScreens, onEnded);
        OverlayLook colorOnly = OverlayLook.ForColor(OpaqueBackground(backdrop.ColorHex));

        if (backdrop.ImagePath is { } imagePath)
        {
            _ = ShowWithImageAsync(request, imagePath, backdrop.ImageDimPercent, colorOnly);
            return;
        }

        PostWindows(request, colorOnly);
    }

    /// <inheritdoc />
    public void Close()
    {
        if (!_isOverlayOpen)
        {
            return;
        }

        _isOverlayOpen = false;
        _ = Interlocked.Increment(ref _generation);

        _ui.Post(_ui.CloseWindows);
    }

    // Das Bild wird gelesen, bevor ein Fenster entsteht: Taugt es nicht, steht das Overlay gleich
    // in der Farbe da, statt erst leer und dann ohne Bild. Der Bildprüfer meldet einen Fehlschlag
    // selbst mit Grund; scheitert er unerwartet, gilt dasselbe wie für ein unlesbares Bild.
    private async Task ShowWithImageAsync(OverlayRequest request, string imagePath, int dimPercent, OverlayLook colorOnly)
    {
        OverlayLook look = colorOnly;
        try
        {
            OverlayImageInfo? info = await _imageInspector.InspectAsync(imagePath).ConfigureAwait(false);
            if (info is not null)
            {
                look = OverlayLook.WithImage(colorOnly.Color, imagePath, info, dimPercent);
            }
        }
        catch (Exception ex) when (LogImageFailure(ex))
        {
            // Der Filter hat protokolliert; das Overlay bleibt bei der Farbe.
        }

        PostWindows(request, look);
    }

    private void PostWindows(OverlayRequest request, OverlayLook look)
        => _ui.Post(() =>
        {
            if (request.Generation == Volatile.Read(ref _generation))
            {
                TryShowWindows(request, look);
            }
        });

    // Erzeugt die Overlay-Fenster auf dem UI-Thread. Scheitert der Aufbau (z. B. ein
    // Fenster-/Vollbild-Fehler auf ungewöhnlichen Monitor-Konfigurationen), wird
    // sauber aufgeräumt und das Pausenende signalisiert — so bleibt die App bedienbar,
    // statt mit gesetztem Overlay-Flag und aktiven Pause-Aktionen hängenzubleiben.
    private void TryShowWindows(OverlayRequest request, OverlayLook look)
    {
        try
        {
            BreakOverlayViewModel viewModel = new(BreakModelRegistry.GetHintKey(request.Model), request.BreakDuration);
            viewModel.Ended += (_, e) => EndOverlay(request.OnEnded, e.Reason);

            _ui.OpenWindows(viewModel, look, request.AllScreens);
            _ui.StartCountdown(() =>
            {
                viewModel.Tick();
                _ui.RefreshWindows();
            });
        }
        catch (Exception ex) when (LogWindowFailure(ex))
        {
            // Als reguläres Pausenende behandeln: Helligkeit/Medien werden
            // zurückgenommen und eine frische Arbeitsphase gestartet.
            EndOverlay(request.OnEnded, BreakEndReason.Elapsed);
        }
    }

    // Deckend rechnen, bevor die Farbe an die Fenster geht: Das Overlay-Fenster ist nicht
    // durchsichtig, eine Alpha-Angabe könnte also gar nichts durchscheinen lassen — sie würde nur
    // gegen den Fenstergrund gemischt, und der ist je nach Monitor nicht derselbe. So zeigen alle
    // Monitore dieselbe Fläche, und die Schriftfarbe rechnet gegen genau diese Farbe.
    private static (byte R, byte G, byte B) OpaqueBackground(string overlayColorHex)
        => HexColor.FlattenOverWhite(HexColor.ParseOrDefault(overlayColorHex));

    private void EndOverlay(Action<BreakEndReason> onEnded, BreakEndReason reason)
    {
        _ui.CloseWindows();
        _isOverlayOpen = false;
        onEnded(reason);
    }

    // Der Overlay-Aufbau darf die App nie hängen lassen. Der Filter protokolliert den
    // Fehler und lässt anschließend den Aufräumpfad laufen.
    private bool LogWindowFailure(Exception exception)
    {
        BreakOverlayPresenterLog.WindowCreationFailed(_logger, exception);
        return true;
    }

    // Ein Bild ist Schmuck, keine Voraussetzung der Pause: Was auch immer beim Lesen scheitert,
    // wird protokolliert, und das Overlay zeigt die Farbe.
    private bool LogImageFailure(Exception exception)
    {
        BreakOverlayPresenterLog.ImageInspectionFailed(_logger, exception);
        return true;
    }

    // Alles, was ein verzögerter Fensteraufbau von seinem Show noch braucht.
    private sealed record OverlayRequest(int Generation, BreakModel Model, TimeSpan BreakDuration, bool AllScreens, Action<BreakEndReason> OnEnded);
}

/// <summary>
/// Source-generierte Logging-Methoden des Overlay-Presenters.
/// </summary>
internal static partial class BreakOverlayPresenterLog
{
    [LoggerMessage(EventId = 1820, Level = LogLevel.Error, Message = "Pausen-Overlay konnte nicht erzeugt werden; die Pause wird als beendet behandelt.")]
    public static partial void WindowCreationFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1821, Level = LogLevel.Error, Message = "Overlay-Bild konnte nicht geprüft werden; das Overlay zeigt die Farbe.")]
    public static partial void ImageInspectionFailed(ILogger logger, Exception exception);
}
