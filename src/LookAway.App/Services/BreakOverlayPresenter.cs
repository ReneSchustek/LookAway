using System;
using System.Collections.Generic;
using System.Globalization;
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

            WinColor background = ParseColor(overlayColorHex);

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

    /// <summary>
    /// Parst eine Hex-Farbe (<c>#RRGGBB</c> oder <c>#AARRGGBB</c>). Bei
    /// ungueltiger Eingabe wird die dunkle Standardfarbe verwendet.
    /// </summary>
    private static WinColor ParseColor(string hex)
    {
        if (!string.IsNullOrEmpty(hex)
            && hex[0] == '#'
            && (hex.Length is 7 or 9))
        {
            string body = hex[1..];
            if (uint.TryParse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                byte a = body.Length == 8 ? (byte)((value >> 24) & 0xFF) : (byte)0xFF;
                byte r = (byte)((value >> 16) & 0xFF);
                byte g = (byte)((value >> 8) & 0xFF);
                byte b = (byte)(value & 0xFF);
                return WinColor.FromArgb(a, r, g, b);
            }
        }

        // Fallback: dunkles, leicht transparentes Standard-Overlay (#F20F1115).
        return WinColor.FromArgb(0xF2, 0x0F, 0x11, 0x15);
    }
}
