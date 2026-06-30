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
/// Abgedunkeltes Vollbild-Overlay während einer Pause — eine Instanz je Monitor.
/// Bindet an das UI-freie <see cref="BreakOverlayViewModel"/>, zeigt einen
/// Sekunden-Countdown und lässt sich mit ESC vorzeitig beenden. Der Countdown
/// wird zentral vom <see cref="Services.BreakOverlayPresenter"/> getaktet, damit
/// mehrere Fenster synchron bleiben und der Zähler nicht mehrfach läuft.
/// </summary>
internal sealed partial class BreakOverlayWindow : Window
{
    private readonly BreakOverlayViewModel _viewModel;
    private bool _closedByCaller;

    /// <summary>
    /// Erzeugt ein Overlay-Fenster für das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Gemeinsame Countdown- und Aktionslogik der Pause.</param>
    /// <param name="localization">Liefert die sprachabhängigen Texte.</param>
    /// <param name="background">Hintergrundfarbe des Overlays (inkl. Transparenz).</param>
    public BreakOverlayWindow(BreakOverlayViewModel viewModel, ILocalizationService localization, WinColor background)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localization);
        _viewModel = viewModel;

        InitializeComponent();

        Title = "LookAway";
        RootGrid.Background = new SolidColorBrush(background);

        // Textfarbe an die gewählte Overlay-Farbe anpassen: auf hellen Farben
        // dunkler Text, sonst heller — damit der Inhalt immer lesbar bleibt.
        ApplyReadableForeground(background);

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

    private void ApplyReadableForeground(WinColor background)
    {
        bool light = Core.Domain.HexColor.IsLight(background.R, background.G, background.B);

        SolidColorBrush primary = new(light ? WinColor.FromArgb(0xFF, 0x0B, 0x1F, 0x1C) : WinColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        SolidColorBrush secondary = new(light ? WinColor.FromArgb(0xFF, 0x33, 0x4A, 0x45) : WinColor.FromArgb(0xFF, 0xE5, 0xE7, 0xEB));
        SolidColorBrush tertiary = new(light ? WinColor.FromArgb(0xFF, 0x52, 0x70, 0x6A) : WinColor.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));

        TitleText.Foreground = primary;
        CountdownText.Foreground = primary;
        HintText.Foreground = secondary;
        EndHintText.Foreground = tertiary;
    }

    /// <summary>
    /// Schließt das Overlay von aussen (reguläres Pausenende oder weil ein
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
