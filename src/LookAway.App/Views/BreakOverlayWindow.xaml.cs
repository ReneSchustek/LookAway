using System;
using LookAway.Application.Localization;
using LookAway.Application.ViewModels;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace LookAway.Views;

/// <summary>
/// Abgedunkeltes Vollbild-Overlay waehrend einer Pause. Bindet an das UI-freie
/// <see cref="BreakOverlayViewModel"/>, zeigt einen Sekunden-Countdown und laesst
/// sich mit ESC vorzeitig beenden. Texte kommen aus der Lokalisierung.
/// </summary>
internal sealed partial class BreakOverlayWindow : Window
{
    private readonly BreakOverlayViewModel _viewModel;
    private DispatcherQueueTimer? _countdownTimer;
    private bool _closedByCaller;

    /// <summary>
    /// Erzeugt das Overlay fuer das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Countdown- und Aktionslogik der Pause.</param>
    /// <param name="localization">Liefert die sprachabhaengigen Texte.</param>
    public BreakOverlayWindow(BreakOverlayViewModel viewModel, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localization);
        _viewModel = viewModel;

        InitializeComponent();

        Title = "LookAway";
        TitleText.Text = localization.GetText(OverlayTextKeys.Title);
        HintText.Text = localization.GetText(viewModel.HintKey);
        EndHintText.Text = localization.GetText(OverlayTextKeys.EndHint);
        CountdownText.Text = viewModel.RemainingDisplay;

        _viewModel.Ended += OnViewModelEnded;
        Closed += OnWindowClosed;

        ConfigureWindow();
        StartCountdown();
    }

    private void ConfigureWindow()
    {
        // Vollbild ueber alle anderen Fenster — der Bildschirm wird abgedunkelt.
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
    }

    private void StartCountdown()
    {
        _countdownTimer = DispatcherQueue.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.IsRepeating = true;
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    private void OnCountdownTick(DispatcherQueueTimer sender, object args)
    {
        _viewModel.Tick();
        CountdownText.Text = _viewModel.RemainingDisplay;
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _viewModel.EndByUser();
    }

    /// <summary>
    /// Schliesst das Overlay von aussen (z. B. weil die Timer-Engine das
    /// regulaere Pausenende gemeldet hat). Unterdrueckt das Benutzer-Ende-Fallback,
    /// damit kein vorzeitiges Ende signalisiert wird.
    /// </summary>
    public void CloseFromCaller()
    {
        _closedByCaller = true;
        StopCountdown();
        Close();
    }

    private void OnViewModelEnded(object? sender, BreakOverlayEndedEventArgs e)
    {
        StopCountdown();
        Close();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        StopCountdown();
        _viewModel.Ended -= OnViewModelEnded;
        Closed -= OnWindowClosed;

        // Vom Aufrufer geschlossen (regulaeres Pausenende): kein Benutzer-Ende
        // signalisieren — der Aufrufer hat die Wiederherstellung bereits angestossen.
        if (_closedByCaller)
        {
            return;
        }

        // Direkt ueber das Fenster geschlossen (ohne ESC/Ablauf): wie ein
        // Benutzer-Ende behandeln, damit Helligkeit/Medien wiederhergestellt werden.
        if (!_viewModel.IsEnded)
        {
            _viewModel.EndByUser();
        }
    }

    private void StopCountdown()
    {
        if (_countdownTimer is null)
        {
            return;
        }

        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
        _countdownTimer = null;
    }
}
