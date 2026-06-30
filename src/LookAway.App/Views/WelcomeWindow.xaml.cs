using System;
using LookAway.Application.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace LookAway.Views;

/// <summary>
/// First-Run-Wizard-Fenster. Bindet an das UI-freie
/// <see cref="WelcomeViewModel"/>; die Ablauflogik liegt dort.
/// </summary>
internal sealed partial class WelcomeWindow : Window
{
    private const int WindowWidth = 540;
    private const int WindowHeight = 560;

    private readonly WelcomeViewModel _viewModel;

    /// <summary>
    /// Erzeugt das Fenster für das angegebene ViewModel.
    /// </summary>
    /// <param name="viewModel">Wizard-ViewModel.</param>
    public WelcomeWindow(WelcomeViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        InitializeComponent();

        RootGrid.DataContext = viewModel;
        Title = viewModel.Title;

        _viewModel.Completed += OnCompleted;

        ConfigureWindow();
    }

    private void ConfigureWindow()
    {
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
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

    private void OnCompleted(object? sender, SettingsAppliedEventArgs e)
    {
        _viewModel.Completed -= OnCompleted;
        Close();
    }
}
