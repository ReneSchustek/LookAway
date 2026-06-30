using System;
using System.Collections.Generic;
using LookAway.Application.Coordination;
using LookAway.Application.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using WinColor = Windows.UI.Color;

namespace LookAway.Services;

/// <summary>
/// WinUI-Implementierung von <see cref="IBreakOverlayPresenter"/>: erzeugt je
/// Monitor ein <see cref="Views.BreakOverlayWindow"/> auf dem UI-Thread und taktet
/// den gemeinsamen Countdown. Auf Wunsch wird jeder angeschlossene Monitor mit
/// einem eigenen Overlay abgedeckt; der Countdown bleibt über alle Fenster
/// synchron. Stellt sicher, dass nie mehrere Overlay-Sätze gleichzeitig offen sind.
/// </summary>
internal sealed class BreakOverlayPresenter : IBreakOverlayPresenter
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ILocalizationService _localization;
    private readonly List<Views.BreakOverlayWindow> _windows = new();
    private DispatcherQueueTimer? _countdownTimer;
    private BreakOverlayViewModel? _viewModel;
    // Wird auf dem UI-Thread gesetzt, aber vom Timer-Consumer-Thread gelesen
    // (BreakCompletedEvent-Gate) — daher volatile fuer korrekte Sichtbarkeit.
    private volatile bool _isOverlayOpen;

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
