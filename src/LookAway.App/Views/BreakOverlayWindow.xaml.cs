using System;
using LookAway.Application.Localization;
using LookAway.Application.ViewModels;
using LookAway.Core.Interfaces;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinColor = Windows.UI.Color;

namespace LookAway.Views;

/// <summary>
/// Abgedunkeltes Vollbild-Overlay waehrend einer Pause — eine Instanz je Monitor.
/// Bindet an das UI-freie <see cref="BreakOverlayViewModel"/>, zeigt einen
/// Sekunden-Countdown und laesst sich mit ESC vorzeitig beenden. Der Countdown
/// wird zentral vom <see cref="Services.BreakOverlayPresenter"/> getaktet, damit
/// mehrere Fenster synchron bleiben und der Zaehler nicht mehrfach laeuft.
/// </summary>
internal sealed partial class BreakOverlayWindow : Window
{
    private readonly BreakOverlayViewModel _viewModel;
    private bool _closedByCaller;

    /// <summary>
    /// Erzeugt ein Overlay-Fenster fuer das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Gemeinsame Countdown- und Aktionslogik der Pause.</param>
    /// <param name="localization">Liefert die sprachabhaengigen Texte.</param>
    /// <param name="background">Hintergrundfarbe des Overlays (inkl. Transparenz).</param>
    public BreakOverlayWindow(BreakOverlayViewModel viewModel, ILocalizationService localization, WinColor background)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localization);
        _viewModel = viewModel;

        InitializeComponent();

        Title = "LookAway";
        RootGrid.Background = new SolidColorBrush(background);
        TitleText.Text = localization.GetText(OverlayTextKeys.Title);
        HintText.Text = localization.GetText(viewModel.HintKey);
        EndHintText.Text = localization.GetText(OverlayTextKeys.EndHint);
        CountdownText.Text = viewModel.RemainingDisplay;

        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Platziert das Fenster auf dem angegebenen Anzeigebereich und schaltet es
    /// dort in den Vollbildmodus, sodass der gesamte Monitor abgedeckt wird.
    /// </summary>
    /// <param name="area">Zielmonitor.</param>
    public void ShowOnDisplay(DisplayArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        // Erst auf den Zielmonitor verschieben, dann Vollbild — der FullScreen-
        // Presenter nutzt den Monitor, auf dem das Fenster gerade liegt.
        RectInt32 bounds = area.OuterBounds;
        AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        Activate();
    }

    /// <summary>Aktualisiert die angezeigte Restzeit aus dem ViewModel.</summary>
    public void RefreshCountdown() => CountdownText.Text = _viewModel.RemainingDisplay;

    /// <summary>
    /// Schliesst das Overlay von aussen (regulaeres Pausenende oder weil ein
    /// Geschwister-Fenster die Pause beendet hat). Unterdrueckt das
    /// Benutzer-Ende-Fallback, damit kein zusaetzliches Ende signalisiert wird.
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

        // Vom Presenter geschlossen (regulaeres Ende, ESC oder Geschwister-Fenster):
        // das Ende ist bereits behandelt.
        if (_closedByCaller)
        {
            return;
        }

        // Direkt ueber das Fenster geschlossen: wie ein Benutzer-Ende behandeln,
        // damit Helligkeit/Medien wiederhergestellt werden.
        if (!_viewModel.IsEnded)
        {
            _viewModel.EndByUser();
        }
    }
}
