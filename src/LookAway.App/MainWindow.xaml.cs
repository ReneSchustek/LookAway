using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;

namespace LookAway;

/// <summary>
/// Hauptfenster der Anwendung.
/// Spaeter wird das Fenster beim Start versteckt und nur das Tray-Icon ist sichtbar.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WinUI-3-XAML-Compiler erfordert eine 'public partial'-Window-Klasse fuer die generierte InitializeComponent-Methode.")]
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
