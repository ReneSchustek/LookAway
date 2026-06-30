using System;
using System.Collections.Generic;
using LookAway.Application.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using WinColor = Windows.UI.Color;

namespace LookAway.Services;

/// <summary>
/// Zeigt waehrend einer Pause das abdunkelnde Vollbild-Overlay. Auf Wunsch wird
/// jeder angeschlossene Monitor mit einem eigenen Overlay abgedeckt; der
/// Countdown laeuft zentral und bleibt so ueber alle Fenster synchron. Stellt
/// sicher, dass nie mehrere Overlay-Saetze gleichzeitig offen sind, und meldet
/// ueber den Callback, wie die Pause endete.
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
    /// <param name="overlayColorHex">Hintergrundfarbe als <c>#AARRGGBB</c>/<c>#RRGGBB</c>.</param>
    /// <param name="allScreens">Alle Monitore abdecken (<c>true</c>) oder nur den Hauptbildschirm.</param>
    /// <param name="onEnded">Callback mit dem Grund des Pausenendes.</param>
    void Show(BreakModel model, TimeSpan breakDuration, string overlayColorHex, bool allScreens, Action<BreakEndReason> onEnded);

    /// <summary>
    /// Schliesst offene Overlays, weil die Timer-Engine das regulaere Pausenende
    /// gemeldet hat. Loest den Callback nicht aus.
    /// </summary>
    void Close();
}

/// <summary>
/// WinUI-Implementierung von <see cref="IBreakOverlayPresenter"/>: erzeugt je
/// Monitor ein <see cref="Views.BreakOverlayWindow"/> auf dem UI-Thread und
/// taktet den gemeinsamen Countdown.
/// </summary>
internal sealed class BreakOverlayPresenter : IBreakOverlayPresenter
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ILocalizationService _localization;
    private readonly List<Views.BreakOverlayWindow> _windows = new();
    private DispatcherQueueTimer? _countdownTimer;
    private BreakOverlayViewModel? _viewModel;
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
    public void Show(BreakModel model, TimeSpan breakDuration, string overlayColorHex, bool allScreens, Action<BreakEndReason> onEnded)
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
            _viewModel = viewModel;
            viewModel.Ended += (_, e) =>
            {
                BreakEndReason reason = e.Reason;
                TeardownWindows();
                _isOverlayOpen = false;
                onEnded(reason);
            };

            (byte a, byte r, byte g, byte b) = HexColor.ParseOrDefault(overlayColorHex);
            WinColor background = WinColor.FromArgb(a, r, g, b);

            // Auf Wunsch jeden Monitor abdecken; sonst nur den Hauptbildschirm.
            IReadOnlyList<DisplayArea> areas = allScreens
                ? DisplayArea.FindAll()
                : new[] { DisplayArea.Primary };

            foreach (DisplayArea area in areas)
            {
                Views.BreakOverlayWindow window = new(viewModel, _localization, background);
                _windows.Add(window);
                window.ShowOnDisplay(area);
            }

            StartCountdown(viewModel);
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

        _ = _dispatcher.TryEnqueue(TeardownWindows);
    }

    private void StartCountdown(BreakOverlayViewModel viewModel)
    {
        _countdownTimer = _dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.IsRepeating = true;
        _countdownTimer.Tick += (_, _) =>
        {
            viewModel.Tick();
            foreach (Views.BreakOverlayWindow window in _windows)
            {
                window.RefreshCountdown();
            }
        };
        _countdownTimer.Start();
    }

    private void TeardownWindows()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        _viewModel = null;

        foreach (Views.BreakOverlayWindow window in _windows)
        {
            window.CloseFromCaller();
        }

        _windows.Clear();
    }
}
