using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.App.Services;
using LookAway.App.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using WinColor = Windows.UI.Color;

namespace LookAway.App.Views;

/// <summary>
/// Settings-Fenster mit Seitenmenü (<see cref="NavigationView"/>). Bindet an das
/// UI-freie <see cref="SettingsViewModel"/>; die gesamte Lade-, Validierungs- und
/// Persistenzlogik liegt dort. Die Auswahl im Menü blendet den passenden
/// Abschnitt ein.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Fenster-Code-Behind: legt WinUI-Elemente an und braucht den Fenster-Thread. Entscheidungen stehen in prüfbaren Klassen (SettingsViewModel, HotkeyCaptureKeys, WindowPlacement).")]
internal sealed partial class SettingsWindow : Window
{
    // Startgröße; danach lässt sich das Fenster frei ziehen, nur nicht maximieren.
    private const int WindowWidth = 840;
    private const int WindowHeight = 680;

    private readonly SettingsViewModel _viewModel;
    private readonly Dictionary<string, FrameworkElement> _panels;
    private bool _suppressColorWriteback;

    /// <summary>
    /// Erzeugt das Fenster für das angegebene (bereits geladene) ViewModel.
    /// </summary>
    /// <param name="viewModel">Bereits geladenes Settings-ViewModel.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        InitializeComponent();

        RootGrid.DataContext = viewModel;
        Title = viewModel.Texts.Title;

        _panels = CreatePanelMap();
        ShowStoredOverlayColor();

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.Statistics.CsvExportRequested += OnCsvExportRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnWindowClosed;

        ConfigureWindow();
    }

    // Die Navigation trägt je Eintrag ein Kennwort; es wählt die anzuzeigende Seite.
    private Dictionary<string, FrameworkElement> CreatePanelMap() => new(StringComparer.Ordinal)
    {
        ["General"] = PanelGeneral,
        ["Model"] = PanelModel,
        ["Intervals"] = PanelIntervals,
        ["Sound"] = PanelSound,
        ["Pause"] = PanelPause,
        ["Hotkeys"] = PanelHotkeys,
        ["Tasks"] = PanelTasks,
        ["Statistics"] = PanelStatistics,
        ["Log"] = PanelLog,
        ["About"] = PanelAbout,
    };

    // Farbwähler auf die gespeicherte Overlay-Farbe setzen (Load lief bereits), ohne dass das
    // Setzen selbst als Änderung zurückgeschrieben wird.
    private void ShowStoredOverlayColor()
    {
        _suppressColorWriteback = true;
        OverlayColorPicker.Color = ParseColor(_viewModel.BreakOverlayColor);
        _suppressColorWriteback = false;
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
        RectInt32 work = DisplayArea.Primary.WorkArea;
        (int left, int top) = WindowPlacement.CenterIn(work.X, work.Y, work.Width, work.Height, WindowWidth, WindowHeight);
        AppWindow.Move(new PointInt32(left, top));
    }

    private void OnNavSelectionChanged(NavigationView _, NavigationViewSelectionChangedEventArgs args)
    {
        // Kann während InitializeComponent feuern, bevor das Panel-Verzeichnis steht.
        if (_panels is null || args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        foreach (KeyValuePair<string, FrameworkElement> entry in _panels)
        {
            entry.Value.Visibility = entry.Key == tag ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnOverlayColorChanged(ColorPicker _, ColorChangedEventArgs args)
    {
        if (_suppressColorWriteback)
        {
            return;
        }

        _viewModel.BreakOverlayColor = ToHex(args.NewColor);
    }

    /// <summary>Formatiert eine Farbe als <c>#AARRGGBB</c>.</summary>
    private static string ToHex(WinColor color) => HexColor.ToHex(color.A, color.R, color.G, color.B);

    /// <summary>
    /// Parst <c>#RRGGBB</c>/<c>#AARRGGBB</c>; bei ungültiger Eingabe wird ein
    /// dunkles Standard-Overlay verwendet.
    /// </summary>
    private static WinColor ParseColor(string hex)
    {
        (byte a, byte r, byte g, byte b) = HexColor.ParseOrDefault(hex);
        return WinColor.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Stellt das Fenster sofort auf das gewählte Erscheinungsbild um.
    /// </summary>
    /// <remarks>
    /// Eine Wahl, die erst nach dem Speichern sichtbar wird, lässt sich nicht
    /// beurteilen. Gespeichert wird sie trotzdem erst mit „Übernehmen" oder
    /// „Speichern"; „Abbrechen" schließt das Fenster und der nächste Aufbau folgt
    /// wieder der gespeicherten Einstellung.
    /// </remarks>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Ein leerer Name bedeutet "alles hat sich geändert" (Sprachwechsel, Laden).
        if (e.PropertyName is nameof(SettingsViewModel.SelectedThemeOption) or "" or null)
        {
            ThemeService.Apply(this, _viewModel.SelectedTheme);
        }
    }

    private async void OnOpenDocs(object _, RoutedEventArgs __)
        => await OpenLinkAsync(_viewModel.DocsUri).ConfigureAwait(true);

    private async void OnOpenDonation(object _, RoutedEventArgs __)
        => await OpenLinkAsync(_viewModel.DonationUri).ConfigureAwait(true);

    private async void OnOpenDownload(object _, RoutedEventArgs __)
        => await OpenLinkAsync(_viewModel.DownloadUri).ConfigureAwait(true);

    /// <summary>
    /// Öffnet einen Verweis über den Starter, der nur http und https zulässt.
    /// </summary>
    /// <remarks>
    /// Als async-void-Ereignisbehandlung muss der gesamte Rumpf gegen Ausnahmen
    /// abgesichert sein — eine unbehandelte Ausnahme würde den Prozess beenden.
    /// Ein Verweis, der sich nicht öffnen lässt, ist das nicht wert.
    /// </remarks>
    private static async Task OpenLinkAsync(Uri? uri)
    {
        try
        {
            _ = await SafeLinkLauncher.OpenAsync(uri).ConfigureAwait(true);
        }
        catch (COMException)
        {
            // Die Shell meldet bei fehlender Zuordnung einen COM-Fehler; dann
            // passiert nichts, und das ist die richtige Antwort.
        }
    }

    /// <summary>
    /// Legt eine Aufgabe auch per Eingabetaste an — wer tippt, will nicht zur Maus
    /// greifen, um den Satz abzuschließen.
    /// </summary>
    private void OnNewTaskKeyDown(object _, KeyRoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key != VirtualKey.Enter || !_viewModel.Tasks.AddCommand.CanExecute(null))
        {
            return;
        }

        e.Handled = true;
        _viewModel.Tasks.AddCommand.Execute(null);
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnCaptureStartBreak(object _, RoutedEventArgs __)
        => _viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

    private void OnCaptureSkipOrSnooze(object _, RoutedEventArgs __)
        => _viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.SkipOrSnooze);

    private void OnCaptureToggleDnd(object _, RoutedEventArgs __)
        => _viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);

    /// <summary>
    /// Nimmt den Tastendruck während einer laufenden Aufnahme entgegen und reicht
    /// ihn unbewertet an das ViewModel weiter. Hier steht bewusst keine Logik: Was
    /// eine Kombination taugt, entscheidet <see cref="SettingsViewModel"/> — nur so
    /// bleibt die Regel ohne Fenster prüfbar.
    /// </summary>
    private void OnHotkeyCaptureKeyDown(object _, KeyRoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!_viewModel.IsCapturingHotkey)
        {
            return;
        }

        // Modifikatoren allein sind noch keine Kombination — sonst wäre jede
        // Aufnahme schon mit dem Druck auf Strg beendet.
        if (HotkeyCaptureKeys.IsModifier(e.Key))
        {
            return;
        }

        e.Handled = true;

        if (e.Key == VirtualKey.Escape)
        {
            _viewModel.CancelHotkeyCaptureCommand.Execute(null);
            return;
        }

        // Das Ergebnis interessiert hier nicht: Ob übernommen, abgelehnt oder
        // kollidiert — die Rückmeldung steht danach im Hinweistext des ViewModels.
        _ = _viewModel.TryCompleteHotkeyCapture(new HotkeyDefinition(HotkeyCaptureKeys.ReadModifiers(IsDown), (int)e.Key));
    }

    private static bool IsDown(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Statistics.CsvExportRequested -= OnCsvExportRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Closed -= OnWindowClosed;
        _viewModel.Dispose();
    }
}
