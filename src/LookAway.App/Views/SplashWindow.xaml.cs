using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using LookAway.App.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Storage.Streams;

namespace LookAway.App.Views;

/// <summary>
/// Startbild, das steht, solange LookAway Einstellungen liest und Tray und Timer einrichtet.
/// Ohne Rahmen und Bedienung, mittig auf dem Hauptbildschirm.
/// </summary>
/// <remarks>
/// Das Logo kommt als eingebettete Ressource aus der Assembly, nicht als Datei neben der EXE:
/// Ein Startbild, das an einer fehlenden oder ausgetauschten Datei scheitert, wäre schlimmer
/// als keines.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Fenster-Code-Behind: legt WinUI-Elemente an und braucht den Fenster-Thread. Entscheidungen stehen in prüfbaren Klassen (SplashPresenter, WindowPlacement).")]
internal sealed partial class SplashWindow : Window, ISplashView
{
    // Quadratisch genug für Logo, Namen und Version; kleiner als jedes andere Fenster der
    // App, damit es nicht wie ein Dialog wirkt, der eine Antwort erwartet.
    private const int WindowWidth = 320;
    private const int WindowHeight = 320;

    private const string LogoResourceName = "LookAway.App.Assets.LookAwayLogo.png";

    /// <summary>Erzeugt das Startbild mit der angezeigten Programmversion.</summary>
    /// <param name="version">Versionsangabe unter dem Produktnamen.</param>
    public SplashWindow(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        InitializeComponent();
        Title = "LookAway";
        VersionText.Text = version;
        ConfigureWindow();
    }

    /// <inheritdoc />
    public async Task ShowAsync()
    {
        await LoadLogoAsync().ConfigureAwait(true);
        Activate();
    }

    // Lädt das Logo aus der eingebetteten Ressource. Fehlt sie, bleibt die Fläche leer;
    // am Start der App ändert das nichts, und ein Test hält die Ressource ohnehin fest.
    private async Task LoadLogoAsync()
    {
        using Stream? resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResourceName);
        if (resource is null)
        {
            return;
        }

        using InMemoryRandomAccessStream buffer = new();
        await resource.CopyToAsync(buffer.AsStreamForWrite()).ConfigureAwait(true);
        buffer.Seek(0);

        BitmapImage logo = new();
        await logo.SetSourceAsync(buffer);
        LogoImage.Source = logo;
    }

    private void ConfigureWindow()
    {
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        RectInt32 work = DisplayArea.Primary.WorkArea;
        (int left, int top) = WindowPlacement.CenterIn(work.X, work.Y, work.Width, work.Height, WindowWidth, WindowHeight);
        AppWindow.Move(new PointInt32(left, top));
    }
}
