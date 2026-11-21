using System.Diagnostics.CodeAnalysis;
using LookAway.App.Services;
using LookAway.App.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace LookAway.App.Views;

/// <summary>
/// First-Run-Wizard-Fenster. Bindet an das UI-freie
/// <see cref="WelcomeViewModel"/>; die Ablauflogik liegt dort.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Fenster-Code-Behind: legt WinUI-Elemente an und braucht den Fenster-Thread. Entscheidungen stehen in prüfbaren Klassen (WelcomeViewModel, WelcomePresenter, WindowPlacement).")]
internal sealed partial class WelcomeWindow : Window
{
    // Feste Größe, zugeschnitten auf den Inhalt der Schritte; das Fenster lässt sich
    // nicht ziehen, also muss jeder Schritt ohne Bildlauf hineinpassen.
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
        RectInt32 work = DisplayArea.Primary.WorkArea;
        (int left, int top) = WindowPlacement.CenterIn(work.X, work.Y, work.Width, work.Height, WindowWidth, WindowHeight);
        AppWindow.Move(new PointInt32(left, top));
    }

    private void OnCompleted(object? sender, SettingsAppliedEventArgs e)
    {
        _viewModel.Completed -= OnCompleted;
        Close();
    }
}
