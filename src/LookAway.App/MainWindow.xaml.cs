using Microsoft.UI.Xaml;

namespace LookAway.App;

/// <summary>
/// Hauptfenster der Anwendung. Bleibt beim Start verborgen — die App lebt im Tray-Icon.
/// </summary>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initialisiert das Hauptfenster.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
    }
}
