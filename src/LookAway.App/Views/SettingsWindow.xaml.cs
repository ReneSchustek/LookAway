using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LookAway.Application.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace LookAway.Views;

/// <summary>
/// Settings-Fenster. Bindet an das UI-freie
/// <see cref="SettingsViewModel"/>; die gesamte Lade-, Validierungs- und
/// Persistenzlogik liegt dort.
/// </summary>
internal sealed partial class SettingsWindow : Window
{
    private const int WindowWidth = 560;
    private const int WindowHeight = 600;

    private static readonly string[] CsvFileExtensions = { ".csv" };

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

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private async void OnCsvExportRequested(object? sender, CsvExportRequestedEventArgs e)
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

        try
        {
            // UTF-8 mit BOM, damit Excel die Datei korrekt erkennt.
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(e.Content);
            await FileIO.WriteBytesAsync(file, bytes);
        }
        catch (IOException)
        {
            // Schreibfehler (z. B. voller Datentraeger) darf den Export nicht
            // zum Absturz bringen — der Benutzer kann erneut versuchen.
        }
        catch (UnauthorizedAccessException)
        {
            // Fehlende Schreibrechte am Zielort — bewusst toleriert.
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
