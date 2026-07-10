using System;
using System.Collections.Generic;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.App.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using WinColor = Windows.UI.Color;

namespace LookAway.App.Services;

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
    private readonly ILogger<BreakOverlayPresenter> _logger;
    private readonly List<Views.BreakOverlayWindow> _windows = new();
    private DispatcherQueueTimer? _countdownTimer;
    // Wird auf dem UI-Thread gesetzt, aber vom Timer-Consumer-Thread gelesen
    // (BreakCompletedEvent-Gate) — daher volatile für korrekte Sichtbarkeit.
    private volatile bool _isOverlayOpen;

    /// <summary>
    /// Erzeugt den Presenter mit dem UI-Dispatcher und der Lokalisierung.
    /// </summary>
    /// <param name="dispatcher">Dispatcher des Hauptfensters.</param>
    /// <param name="localization">Liefert die sprachabhängigen Texte.</param>
    /// <param name="logger">Protokolliert fehlgeschlagene Fenster-Aufbauten.</param>
    public BreakOverlayPresenter(DispatcherQueue dispatcher, ILocalizationService localization, ILogger<BreakOverlayPresenter> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _localization = localization;
        _logger = logger;
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

        _ = _dispatcher.TryEnqueue(() => TryShowWindows(model, breakDuration, overlayColorHex, allScreens, onEnded));
    }

    // Erzeugt die Overlay-Fenster auf dem UI-Thread. Scheitert der Aufbau (z. B. ein
    // Fenster-/Vollbild-Fehler auf ungewöhnlichen Monitor-Konfigurationen), wird
    // sauber aufgeräumt und das Pausenende signalisiert — so bleibt die App bedienbar,
    // statt mit gesetztem Overlay-Flag und aktiven Pause-Aktionen hängenzubleiben.
    private void TryShowWindows(BreakModel model, TimeSpan breakDuration, string overlayColorHex, bool allScreens, Action<BreakEndReason> onEnded)
    {
        try
        {
            string hintKey = BreakModelRegistry.GetHintKey(model);
            BreakOverlayViewModel viewModel = new(hintKey, breakDuration);
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
            // DisplayArea.FindAll() liefert eine WinRT-Projektion, deren Enumeration
            // per foreach in CsWinRT einen InvalidCastException wirft (fehlschlagende
            // IIterable-Abfrage). Daher per Index (IVectorView) in ein verwaltetes
            // Array übernehmen und erst über dieses iterieren.
            DisplayArea primary = DisplayArea.Primary;

            DisplayArea[] areas;
            if (allScreens)
            {
                IReadOnlyList<DisplayArea> found = DisplayArea.FindAll();
                areas = new DisplayArea[found.Count];
                for (int i = 0; i < found.Count; i++)
                {
                    areas[i] = found[i];
                }
            }
            else
            {
                areas = new[] { primary };
            }

            foreach (DisplayArea area in areas)
            {
                // Inhalt (Titel/Hinweis/Countdown) nur auf dem Hauptmonitor; weitere
                // Monitore werden nur abgedunkelt (leeres Overlay).
                bool isPrimary = area.DisplayId.Value == primary.DisplayId.Value;
                Views.BreakOverlayWindow window = new(viewModel, _localization, background, isPrimary);
                _windows.Add(window);
                window.ShowOnDisplay(area);
            }

            StartCountdown(viewModel);
        }
        catch (Exception ex) when (LogWindowFailure(ex))
        {
            TeardownWindows();
            _isOverlayOpen = false;
            // Als reguläres Pausenende behandeln: Helligkeit/Medien werden
            // zurückgenommen und eine frische Arbeitsphase gestartet.
            onEnded(BreakEndReason.Elapsed);
        }
    }

    // Der Overlay-Aufbau darf die App nie hängen lassen. Der Filter protokolliert den
    // Fehler und lässt anschließend den Aufräumpfad laufen.
    private bool LogWindowFailure(Exception exception)
    {
        BreakOverlayPresenterLog.WindowCreationFailed(_logger, exception);
        return true;
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

        foreach (Views.BreakOverlayWindow window in _windows)
        {
            window.CloseFromCaller();
        }

        _windows.Clear();
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Overlay-Presenters.
/// </summary>
internal static partial class BreakOverlayPresenterLog
{
    [LoggerMessage(EventId = 1820, Level = LogLevel.Error, Message = "Pausen-Overlay konnte nicht erzeugt werden; die Pause wird als beendet behandelt.")]
    public static partial void WindowCreationFailed(ILogger logger, Exception exception);
}
