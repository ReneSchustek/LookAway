using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LookAway.Core.Domain;
using LookAway.App.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinColor = Windows.UI.Color;

namespace LookAway.App.Views;

/// <summary>
/// Settings-Fenster mit Seitenmenü (<see cref="NavigationView"/>). Bindet an das
/// UI-freie <see cref="SettingsViewModel"/>; die gesamte Lade-, Validierungs- und
/// Persistenzlogik liegt dort. Die Auswahl im Menü blendet den passenden
/// Abschnitt ein.
/// </summary>
internal sealed partial class SettingsWindow : Window
{
    private const int WindowWidth = 840;
    private const int WindowHeight = 680;

    private static readonly string[] CsvFileExtensions = { ".csv" };

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
        Title = viewModel.Title;

        _panels = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal)
        {
            ["General"] = PanelGeneral,
            ["Model"] = PanelModel,
            ["Intervals"] = PanelIntervals,
            ["Sound"] = PanelSound,
            ["Pause"] = PanelPause,
            ["Hotkeys"] = PanelHotkeys,
            ["Statistics"] = PanelStatistics,
            ["About"] = PanelAbout,
        };

        // Farbwähler auf die gespeicherte Overlay-Farbe setzen (Load lief bereits).
        _suppressColorWriteback = true;
        OverlayColorPicker.Color = ParseColor(viewModel.BreakOverlayColor);
        _suppressColorWriteback = false;

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.Statistics.CsvExportRequested += OnCsvExportRequested;
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

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
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

    private void OnOverlayColorChanged(ColorPicker sender, ColorChangedEventArgs args)
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

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private async void OnCsvExportRequested(object? sender, CsvExportRequestedEventArgs e)
    {
        // Als async-void-Event-Handler muss der gesamte Rumpf gegen Ausnahmen
        // abgesichert sein — eine unbehandelte Ausnahme würde sonst den Prozess beenden.
        try
        {
            FileSavePicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "lookaway-history",
            };
            picker.FileTypeChoices.Add("CSV", CsvFileExtensions);

            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            // UTF-8 mit BOM, damit Excel die Datei korrekt erkennt.
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(e.Content);
            await FileIO.WriteBytesAsync(file, bytes);
        }
        catch (IOException)
        {
            // Schreibfehler (z. B. voller Datenträger) darf den Export nicht
            // zum Absturz bringen — der Benutzer kann erneut versuchen.
        }
        catch (UnauthorizedAccessException)
        {
            // Fehlende Schreibrechte am Zielort — bewusst toleriert.
        }
        catch (COMException)
        {
            // Datei-Dialog/Storage-Interop kann bei Abbruch oder Shell-Fehlern
            // einen COM-Fehler melden — der Export bleibt dann einfach aus.
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Statistics.CsvExportRequested -= OnCsvExportRequested;
        Closed -= OnWindowClosed;
        _viewModel.Dispose();
    }
}
