using System;
using LookAway.Application.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;

namespace LookAway.Services;

/// <summary>
/// Zeigt waehrend einer Pause das abdunkelnde Vollbild-Overlay. Stellt sicher,
/// dass nie mehrere Overlays gleichzeitig offen sind, und meldet ueber den
/// Callback, wie die Pause endete (regulaer abgelaufen oder vom Benutzer beendet).
/// </summary>
internal interface IBreakOverlayPresenter
{
    /// <summary>Ist gerade ein Overlay sichtbar?</summary>
    bool IsOverlayOpen { get; }

    /// <summary>
    /// Zeigt das Pausen-Overlay fuer das Modell und die Pausendauer. Bei bereits
    /// offenem Overlay passiert nichts.
    /// </summary>
    /// <param name="model">Aktives Pausenmodell (fuer den Hinweis-Schluessel).</param>
    /// <param name="breakDuration">Dauer der Pause.</param>
    /// <param name="onEnded">Callback mit dem Grund des Pausenendes.</param>
    void Show(BreakModel model, TimeSpan breakDuration, Action<BreakEndReason> onEnded);

    /// <summary>
    /// Schliesst ein offenes Overlay, weil die Timer-Engine das regulaere
    /// Pausenende gemeldet hat. Loest den Callback nicht aus.
    /// </summary>
    void Close();
}

/// <summary>
/// WinUI-Implementierung von <see cref="IBreakOverlayPresenter"/>: erzeugt das
/// <see cref="Views.BreakOverlayWindow"/> auf dem UI-Thread.
/// </summary>
internal sealed class BreakOverlayPresenter : IBreakOverlayPresenter
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ILocalizationService _localization;
    private Views.BreakOverlayWindow? _window;
    private bool _isOverlayOpen;

    /// <summary>
    /// Erzeugt den Presenter mit dem UI-Dispatcher und der Lokalisierung.
    /// </summary>
    /// <param name="dispatcher">Dispatcher des Hauptfensters.</param>
    /// <param name="localization">Liefert die sprachabhaengigen Texte.</param>
    public BreakOverlayPresenter(DispatcherQueue dispatcher, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(localization);
        _dispatcher = dispatcher;
        _localization = localization;
    }

    /// <inheritdoc />
    public bool IsOverlayOpen => _isOverlayOpen;

    /// <inheritdoc />
    public void Show(BreakModel model, TimeSpan breakDuration, Action<BreakEndReason> onEnded)
    {
        ArgumentNullException.ThrowIfNull(onEnded);

        if (_isOverlayOpen || breakDuration <= TimeSpan.Zero)
        {
            return;
        }

        _isOverlayOpen = true;

        _ = _dispatcher.TryEnqueue(() =>
        {
            string hintKey = BreakModelRegistry.GetHintKey(model);
            BreakOverlayViewModel viewModel = new(hintKey, breakDuration);
            viewModel.Ended += (_, e) =>
            {
                _isOverlayOpen = false;
                _window = null;
                onEnded(e.Reason);
            };

            _window = new Views.BreakOverlayWindow(viewModel, _localization);
            _window.Activate();
        });
    }

    /// <inheritdoc />
    public void Close()
    {
        if (!_isOverlayOpen)
        {
            return;
        }

        _isOverlayOpen = false;

        _ = _dispatcher.TryEnqueue(() =>
        {
            _window?.CloseFromCaller();
            _window = null;
        });
    }
}
