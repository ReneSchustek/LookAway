using System;
using LookAway.Application.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace LookAway.Views;

/// <summary>
/// Settings-Fenster (BRIEF008). Bindet an das UI-freie
/// <see cref="SettingsViewModel"/>; die gesamte Lade-, Validierungs- und
/// Persistenzlogik liegt dort.
/// </summary>
internal sealed partial class SettingsWindow : Window
{
    private const int WindowWidth = 560;
    private const int WindowHeight = 600;

    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// Erzeugt das Fenster fuer das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Bereits geladenes Settings-ViewModel.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        InitializeComponent();

        RootGrid.DataContext = viewModel;
        Title = viewModel.Title;

        _viewModel.CloseRequested += OnCloseRequested;
        Closed += OnWindowClosed;

        ConfigureWindow();
    }

    private void ConfigureWindow()
    {
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = true;
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

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        Closed -= OnWindowClosed;
        _viewModel.Dispose();
    }
}
