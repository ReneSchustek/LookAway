using System;
using LookAway.Application.Localization;
using LookAway.Application.ViewModels;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace LookAway.Views;

/// <summary>
/// Eigenstaendiges, dezentes Erinnerungsfenster. Bindet an das UI-freie
/// <see cref="BreakReminderViewModel"/>; die Texte kommen aus der Lokalisierung.
/// </summary>
internal sealed partial class BreakReminderWindow : Window
{
    private const int WindowWidth = 480;
    private const int WindowHeight = 320;

    private readonly BreakReminderViewModel _viewModel;
    private DispatcherQueueTimer? _timeoutTimer;

    /// <summary>
    /// Erzeugt das Fenster fuer das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Aktionslogik der Erinnerung.</param>
    /// <param name="localization">Liefert die sprachabhaengigen Texte.</param>
    public BreakReminderWindow(BreakReminderViewModel viewModel, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localization);
        _viewModel = viewModel;

        InitializeComponent();

        Title = "LookAway";
        TitleText.Text = localization.GetText(ReminderTextKeys.Title);
        HintText.Text = localization.GetText(viewModel.HintKey);
        StartButton.Content = localization.GetText(ReminderTextKeys.StartBreak);
        SnoozeButton.Content = localization.GetText(ReminderTextKeys.Snooze);
        SkipButton.Content = localization.GetText(ReminderTextKeys.Skip);

        _viewModel.Completed += OnViewModelCompleted;
        Closed += OnWindowClosed;

        ConfigureWindow();
        StartTimeoutTimer();
    }

    private void ConfigureWindow()
    {
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        CenterOnPrimaryDisplay();
    }

    private void CenterOnPrimaryDisplay()
    {
        DisplayArea display = DisplayArea.Primary;
        RectInt32 work = display.WorkArea;
        int left = work.X + ((work.Width - WindowWidth) / 2);
        int top = work.Y + ((work.Height - WindowHeight) / 2);
        AppWindow.Move(new PointInt32(left, top));
    }

    private void StartTimeoutTimer()
    {
        _timeoutTimer = DispatcherQueue.CreateTimer();
        _timeoutTimer.Interval = TimeSpan.FromSeconds(BreakReminderViewModel.DefaultTimeoutSeconds);
        _timeoutTimer.IsRepeating = false;
        _timeoutTimer.Tick += (_, _) => _viewModel.TimeoutElapsed();
        _timeoutTimer.Start();
    }

    private void OnViewModelCompleted(object? sender, ReminderCompletedEventArgs e)
    {
        _timeoutTimer?.Stop();
        _timeoutTimer = null;
        Close();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _timeoutTimer?.Stop();
        _timeoutTimer = null;
        _viewModel.Completed -= OnViewModelCompleted;
        Closed -= OnWindowClosed;

        // Direkt ueber das X geschlossen (keine Aktion gewaehlt): wie "Ueberspringen"
        // behandeln, damit kein Default-"Pause starten" per Timeout nachfeuert.
        if (!_viewModel.IsCompleted)
        {
            _viewModel.Skip();
        }
    }

    private void OnStartBreakClick(object sender, RoutedEventArgs e) => _viewModel.StartBreak();

    private void OnSnoozeClick(object sender, RoutedEventArgs e) => _viewModel.Snooze();

    private void OnSkipClick(object sender, RoutedEventArgs e) => _viewModel.Skip();
}
