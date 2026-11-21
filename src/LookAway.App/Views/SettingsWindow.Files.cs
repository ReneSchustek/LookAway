using System.Runtime.InteropServices;
using System.Text;
using LookAway.Core.Domain;
using LookAway.App.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace LookAway.App.Views;

/// <summary>
/// Die Dateidialoge des Fensters: Speichern des CSV-Exports und Wahl des Overlay-Bilds.
/// </summary>
internal sealed partial class SettingsWindow
{
    private static readonly string[] CsvFileExtensions = { ".csv" };

    private async void OnCsvExportRequested(object? sender, CsvExportRequestedEventArgs e)
    {
        // Als async-void-Event-Handler muss der gesamte Rumpf gegen Ausnahmen
        // abgesichert sein — eine unbehandelte Ausnahme würde sonst den Prozess beenden.
        try
        {
            StorageFile? file = await CreateCsvPicker().PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            // UTF-8 mit BOM, damit Excel die Datei korrekt erkennt.
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(e.Content);
            await FileIO.WriteBytesAsync(file, bytes);
            _viewModel.Statistics.ReportExportSaved();
        }
        // Voller Datenträger, fehlende Schreibrechte oder ein Shell-Fehler im Dateidialog: Der
        // Export bleibt aus, das Fenster bedienbar, und die Rückmeldung sagt es dem Benutzer.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            _viewModel.Statistics.ReportExportFailed(ex);
        }
    }

    // Async void nur als Ereignis-Handler: Ein Fehler des Dateidialogs (Shell-Fehler) darf das
    // Fenster nicht reißen; die Bildwahl bleibt dann einfach, wie sie war.
    private async void OnChooseOverlayImage(object _, RoutedEventArgs __)
    {
        try
        {
            StorageFile? file = await CreateImagePicker().PickSingleFileAsync();
            if (file is not null)
            {
                _ = _viewModel.UseOverlayImage(file.Path);
            }
        }
        catch (COMException ex)
        {
            _viewModel.ReportOverlayImagePickFailed(ex);
        }
    }

    private FileOpenPicker CreateImagePicker()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail,
        };
        foreach (string extension in OverlayImage.SupportedExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        AttachToWindow(picker);
        return picker;
    }

    // Der Dateidialog braucht das Fensterhandle, sonst weiß er nicht, zu welchem Fenster er gehört.
    private void AttachToWindow(object picker)
        => WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

    private FileSavePicker CreateCsvPicker()
    {
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "lookaway-history",
        };
        picker.FileTypeChoices.Add("CSV", CsvFileExtensions);
        AttachToWindow(picker);
        return picker;
    }
}
