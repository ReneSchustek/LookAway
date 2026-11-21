using System.Diagnostics.CodeAnalysis;
using LookAway.App.ViewModels;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;

namespace LookAway.App.Services;

/// <summary>
/// Echte Fensterseite des Pausen-Overlays: ein <see cref="Views.BreakOverlayWindow"/> je Monitor,
/// der Sekundentakt über den Dispatcher des Hauptfensters.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Legt WinUI-Fenster auf echten Monitoren an und braucht den Fenster-Thread. Ablauf und Monitorreihenfolge prüfen BreakOverlayPresenterTests und DisplayOrderTests.")]
internal sealed class BreakOverlayWindows : IBreakOverlayUi
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ILocalizationService _localization;
    private readonly ITopmostWindowGuard _topmost;
    private readonly IWindowFrameSuppressor _frameSuppressor;
    private readonly ILogger<BreakOverlayWindows> _logger;
    private readonly List<Views.BreakOverlayWindow> _windows = new();
    private DispatcherQueueTimer? _countdownTimer;

    /// <summary>Erzeugt die Fensterseite.</summary>
    /// <param name="dispatcher">Dispatcher des Hauptfensters.</param>
    /// <param name="localization">Texte des Overlays.</param>
    /// <param name="topmost">Hält die Fenster in der obersten Lage.</param>
    /// <param name="frameSuppressor">Unterdrückt Rahmen und Schatten der Fenster.</param>
    /// <param name="logger">Protokolliert ein Bild, das sich im Fenster doch nicht laden ließ.</param>
    public BreakOverlayWindows(
        DispatcherQueue dispatcher,
        ILocalizationService localization,
        ITopmostWindowGuard topmost,
        IWindowFrameSuppressor frameSuppressor,
        ILogger<BreakOverlayWindows> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(topmost);
        ArgumentNullException.ThrowIfNull(frameSuppressor);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _localization = localization;
        _topmost = topmost;
        _frameSuppressor = frameSuppressor;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Post(Action action) => _ = _dispatcher.TryEnqueue(() => action());

    /// <inheritdoc />
    public void OpenWindows(BreakOverlayViewModel viewModel, OverlayLook look, bool allScreens)
    {
        DisplayArea primary = DisplayArea.Primary;
        IReadOnlyList<DisplayArea> all = allScreens ? DisplayArea.FindAll() : [primary];

        foreach (DisplayArea area in DisplayOrder.Arrange(primary, all, static a => a.DisplayId.Value, allScreens))
        {
            bool isPrimary = area.DisplayId.Value == primary.DisplayId.Value;
            Views.BreakOverlayWindow window = new(viewModel, _localization, _topmost, _frameSuppressor, look, isPrimary);
            window.ImageFailed += OnImageFailed;
            _windows.Add(window);
            window.ShowOnDisplay(area);
        }
    }

    /// <inheritdoc />
    public void StartCountdown(Action tick)
    {
        _countdownTimer = _dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.IsRepeating = true;
        _countdownTimer.Tick += (_, _) => tick();
        _countdownTimer.Start();
    }

    /// <inheritdoc />
    public void RefreshWindows()
    {
        foreach (Views.BreakOverlayWindow window in _windows)
        {
            window.RefreshCountdown();
            // Jede Sekunde die oberste Lage nachziehen: Fremde Fenster, die sich
            // selbst nach vorn holen, verdecken die Pause damit höchstens kurz.
            window.KeepOnTop();
        }
    }

    /// <inheritdoc />
    public void CloseWindows()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;

        foreach (Views.BreakOverlayWindow window in _windows)
        {
            window.ImageFailed -= OnImageFailed;
            window.CloseFromCaller();
        }

        _windows.Clear();
    }

    // Der Presenter hat das Bild vorher gelesen; scheitert das Fenster trotzdem daran (etwa weil
    // die Datei inzwischen gelöscht ist), zeigt es die Farbe, und das gehört ins Protokoll.
    private void OnImageFailed(object? sender, string reason) => BreakOverlayWindowsLog.ImageFailed(_logger, reason);
}

/// <summary>
/// Source-generierte Logging-Methoden der Overlay-Fenster.
/// </summary>
internal static partial class BreakOverlayWindowsLog
{
    [LoggerMessage(EventId = 1822, Level = LogLevel.Warning, Message = "Overlay-Bild ließ sich im Fenster nicht laden ({Reason}); das Overlay zeigt die Farbe.")]
    public static partial void ImageFailed(ILogger logger, string reason);
}
