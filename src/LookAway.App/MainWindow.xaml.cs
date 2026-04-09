using Microsoft.UI.Xaml;

namespace LookAway;

/// <summary>
/// Hauptfenster der Anwendung.
/// In spaeteren BRIEFs wird das Fenster beim Start versteckt und nur das Tray-Icon ist sichtbar.
/// </summary>
public sealed partial class MainWindow : Window
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
