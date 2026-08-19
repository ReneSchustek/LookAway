using System;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.App.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinColor = Windows.UI.Color;

namespace LookAway.App.Views;

/// <summary>
/// Abgedunkeltes Vollbild-Overlay während einer Pause — eine Instanz je Monitor.
/// Bindet an das UI-freie <see cref="BreakOverlayViewModel"/>, zeigt einen
/// Sekunden-Countdown und lässt sich mit ESC vorzeitig beenden. Der Countdown
/// wird zentral vom <see cref="Services.BreakOverlayPresenter"/> getaktet, damit
/// mehrere Fenster synchron bleiben und der Zähler nicht mehrfach läuft.
/// </summary>
internal sealed partial class BreakOverlayWindow : Window
{
    // Schlüssel der Haupt-Schriftfarbe; die beiden Nebentöne folgen demselben Muster,
    // der Zusatz "OnLight" wählt den Satz für helle Overlay-Farben.
    private const string PrimaryInkKey = "RcOverlayForeground";

    private readonly BreakOverlayViewModel _viewModel;
    private readonly ITopmostWindowGuard _topmost;
    private readonly IWindowFrameSuppressor _frameSuppressor;
    private readonly bool _showContent;
    private bool _closedByCaller;

    /// <summary>
    /// Erzeugt ein Overlay-Fenster für das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Gemeinsame Countdown- und Aktionslogik der Pause.</param>
    /// <param name="localization">Liefert die sprachabhängigen Texte.</param>
    /// <param name="topmost">Hält das Fenster über allen anderen.</param>
    /// <param name="frameSuppressor">Nimmt dem Fenster Randlinie und runde Ecken.</param>
    /// <param name="background">
    /// Deckende Hintergrundfarbe des Overlays. Der <see cref="Services.BreakOverlayPresenter"/>
    /// rechnet eine Alpha-Angabe vorher heraus, damit jeder Monitor dieselbe Fläche zeigt.
    /// </param>
    /// <param name="showContent">
    /// Zeigt dieses Fenster Titel, Hinweis und Countdown? Nur der Hauptmonitor zeigt
    /// den Inhalt; weitere Monitore werden nur abgedunkelt (leeres Overlay).
    /// </param>
    public BreakOverlayWindow(
        BreakOverlayViewModel viewModel,
        ILocalizationService localization,
        ITopmostWindowGuard topmost,
        IWindowFrameSuppressor frameSuppressor,
        WinColor background,
        bool showContent)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(topmost);
        ArgumentNullException.ThrowIfNull(frameSuppressor);
        _viewModel = viewModel;
        _topmost = topmost;
        _frameSuppressor = frameSuppressor;
        _showContent = showContent;

        InitializeComponent();

        Title = "LookAway";
        RootGrid.Background = new SolidColorBrush(background);

        if (showContent)
        {
            // Hauptmonitor: Titel, Hinweis und Countdown anzeigen. Die Textfarbe an
            // die gewählte Overlay-Farbe anpassen (auf hellen Farben dunkler Text,
            // sonst heller), damit der Inhalt immer lesbar bleibt.
            ApplyReadableForeground(background);

            TitleText.Text = localization.GetText(OverlayTextKeys.Title);
            HintText.Text = localization.GetText(viewModel.HintKey);
            EndHintText.Text = localization.GetText(OverlayTextKeys.EndHint);
            CountdownText.Text = viewModel.RemainingDisplay;
        }
        else
        {
            // Nebenmonitor: nur abdunkeln, keinen Inhalt zeigen. Der ESC-Kurzbefehl
            // (auf dem RootGrid) bleibt aktiv, sodass die Pause auch hier endbar ist.
            ContentPanel.Visibility = Visibility.Collapsed;
        }

        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Deckt den angegebenen Monitor vollständig mit dem Overlay ab und zeigt es an.
    /// </summary>
    /// <param name="area">Zielmonitor.</param>
    /// <remarks>
    /// Aktiviert wird jedes Fenster: Erst damit baut WinUI die XAML-Insel auf, ein bloß
    /// gezeigtes Fenster bliebe leer. Wo der Eingabefokus am Ende sitzt, entscheidet
    /// deshalb die Reihenfolge — der <see cref="Services.BreakOverlayPresenter"/> zeigt
    /// den Hauptmonitor zuletzt, sodass ESC dort ankommt.
    /// </remarks>
    public void ShowOnDisplay(DisplayArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        ConfigureAsOverlay();

        RectInt32 bounds = area.OuterBounds;
        AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        Activate();

        // Erst nach dem Aktivieren: Bis dahin setzt WinUI die Fensterstile noch einmal
        // selbst, und ein vorher genommener Rahmen käme zurück.
        _frameSuppressor.SuppressFrame(Win32Interop.GetWindowFromWindowId(AppWindow.Id));
        KeepOnTop();
    }

    /// <summary>
    /// Hebt das Fenster wieder über alle anderen, falls sich eines davor geschoben hat.
    /// Der <see cref="Services.BreakOverlayPresenter"/> ruft das im Sekundentakt.
    /// </summary>
    public void KeepOnTop() => _topmost.BringToTop(Win32Interop.GetWindowFromWindowId(AppWindow.Id));

    private void ConfigureAsOverlay()
    {
        // Kein Vollbild-Presenter mehr: Er fällt zurück, sobald das Fenster den
        // Vordergrund verliert — bei mehreren Monitoren also überall dort, wo der Fokus
        // gerade nicht sitzt. Sichtbar wurde das an der wieder auftauchenden Titelleiste
        // auf den Nebenmonitoren. Ein randloses Fenster in Monitorgröße, das immer oben
        // liegt, hält die Abdeckung unabhängig vom Fokus.
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Kein Eintrag in Taskleiste und Alt-Tab: Sonst stünde LookAway dort einmal je
        // Monitor, und ein Wechsel dorthin würde die Pause verdecken.
        AppWindow.IsShownInSwitchers = false;

    }

    /// <summary>
    /// Aktualisiert die angezeigte Restzeit aus dem ViewModel. Nur auf dem
    /// Hauptmonitor wirksam; Nebenmonitore zeigen keinen Countdown.
    /// </summary>
    public void RefreshCountdown()
    {
        if (_showContent)
        {
            CountdownText.Text = _viewModel.RemainingDisplay;
        }
    }

    private void ApplyReadableForeground(WinColor background)
    {
        // Welcher der beiden Schriftsätze auf der gewählten Overlay-Farbe besser liest,
        // entscheidet das Kontrastverhältnis (LookAway.Core) und keine Helligkeitsschwelle.
        // Die Farbwerte selbst stehen in den Belegungen, nicht hier.
        bool onLight = DarkInkReadsBetter(background);

        TitleText.Foreground = OverlayBrush(PrimaryInkKey, onLight);
        CountdownText.Foreground = OverlayBrush(PrimaryInkKey, onLight);
        HintText.Foreground = OverlayBrush("RcOverlayForegroundMuted", onLight);
        EndHintText.Foreground = OverlayBrush("RcOverlayForegroundFaint", onLight);
    }

    private static bool DarkInkReadsBetter(WinColor background)
    {
        (byte R, byte G, byte B) surface = (background.R, background.G, background.B);
        (byte R, byte G, byte B) darkInk = Channels(OverlayBrush(PrimaryInkKey, onLight: true).Color);
        (byte R, byte G, byte B) lightInk = Channels(OverlayBrush(PrimaryInkKey, onLight: false).Color);

        return HexColor.ContrastRatio(surface, darkInk) > HexColor.ContrastRatio(surface, lightInk);
    }

    private static SolidColorBrush OverlayBrush(string themeKey, bool onLight)
        => (SolidColorBrush)Application.Current.Resources[onLight ? themeKey + "OnLight" : themeKey];

    private static (byte R, byte G, byte B) Channels(WinColor color) => (color.R, color.G, color.B);

    /// <summary>
    /// Schließt das Overlay von außen (reguläres Pausenende oder weil ein
    /// Geschwister-Fenster die Pause beendet hat). Unterdrückt das
    /// Benutzer-Ende-Fallback, damit kein zusätzliches Ende signalisiert wird.
    /// </summary>
    public void CloseFromCaller()
    {
        _closedByCaller = true;
        Close();
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _viewModel.EndByUser();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnWindowClosed;

        // Vom Presenter geschlossen (reguläres Ende, ESC oder Geschwister-Fenster):
        // das Ende ist bereits behandelt.
        if (_closedByCaller)
        {
            return;
        }

        // Direkt über das Fenster geschlossen: wie ein Benutzer-Ende behandeln,
        // damit Helligkeit/Medien wiederhergestellt werden.
        if (!_viewModel.IsEnded)
        {
            _viewModel.EndByUser();
        }
    }
}
