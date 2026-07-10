using System;
using System.Globalization;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.App.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace LookAway.App.Views;

/// <summary>
/// Eigenständiges, dezentes Erinnerungsfenster. Bindet an das UI-freie
/// <see cref="BreakReminderViewModel"/>; die Texte kommen aus der Lokalisierung.
/// </summary>
internal sealed partial class BreakReminderWindow : Window
{
    private const int WindowWidth = 480;
    private const int WindowHeight = 320;

    private readonly BreakReminderViewModel _viewModel;
    private readonly string _countdownTemplate;
    private DispatcherQueueTimer? _countdownTimer;

    /// <summary>
    /// Erzeugt das Fenster für das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Aktionslogik der Erinnerung (inkl. Countdown-Zustand).</param>
    /// <param name="localization">Liefert die sprachabhängigen Texte.</param>
    public BreakReminderWindow(BreakReminderViewModel viewModel, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localization);
        _viewModel = viewModel;
        _countdownTemplate = localization.GetText(ReminderTextKeys.AutoStartCountdown);

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
        StartCountdown();
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

    private void StartCountdown()
    {
        // Ohne konfigurierten Auto-Start bleibt die Erinnerung offen, bis der Benutzer
        // eine Aktion wählt — dann keinen Countdown anzeigen.
        if (!_viewModel.AutoStartsAutomatically)
        {
            return;
        }

        CountdownText.Visibility = Visibility.Visible;
        UpdateCountdownText();

        _countdownTimer = DispatcherQueue.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.IsRepeating = true;
        _countdownTimer.Tick += (_, _) =>
        {
            _viewModel.Tick();
            UpdateCountdownText();
        };
        _countdownTimer.Start();
    }

    private void UpdateCountdownText()
        => CountdownText.Text = string.Format(
            CultureInfo.CurrentCulture, _countdownTemplate, _viewModel.RemainingSeconds);

    private void OnViewModelCompleted(object? sender, ReminderCompletedEventArgs e)
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        Close();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        _viewModel.Completed -= OnViewModelCompleted;
        Closed -= OnWindowClosed;

        // Direkt über das X geschlossen (keine Aktion gewählt): wie "Überspringen"
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
