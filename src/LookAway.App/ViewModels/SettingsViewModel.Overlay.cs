using CommunityToolkit.Mvvm.ComponentModel;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Aussehen des Pausen-Overlays auf der Seite „Pausen-Aktionen“: Farbe oder eigenes Bild, die
/// Bildwahl und die Abdunklung über dem Bild.
/// </summary>
internal sealed partial class SettingsViewModel
{
    [ObservableProperty]
    public partial string BreakOverlayColor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverlayImageSelected))]
    public partial SettingsOption<OverlayBackground>? SelectedOverlayBackgroundOption { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverlayImageName))]
    public partial string? BreakOverlayImagePath { get; set; }

    [ObservableProperty]
    public partial int BreakOverlayImageDimPercent { get; set; }

    /// <summary>Auswählbare Arten des Overlay-Hintergrunds mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<OverlayBackground>> OverlayBackgrounds { get; private set; } = [];

    /// <summary>Ist „Eigenes Bild“ gewählt? Steuert, ob Bildwahl und Abdunklung sichtbar sind.</summary>
    public bool IsOverlayImageSelected => SelectedOverlayBackgroundOption?.Value == OverlayBackground.Image;

    /// <summary>
    /// Dateiname des gewählten Bilds, oder der Hinweis, dass noch keines gewählt ist. Der volle
    /// Pfad bliebe in der schmalen Karte unlesbar.
    /// </summary>
    public string OverlayImageName => string.IsNullOrWhiteSpace(BreakOverlayImagePath)
        ? _localization.GetText(SettingsTextKeys.PauseActionsOverlayImageNone)
        : Path.GetFileName(BreakOverlayImagePath);

    /// <summary>Untere Grenze der Abdunklung über dem Bild.</summary>
    public int OverlayImageDimMin => Settings.MinOverlayImageDimPercent;

    /// <summary>Obere Grenze der Abdunklung über dem Bild.</summary>
    public int OverlayImageDimMax => Settings.MaxOverlayImageDimPercent;

    /// <summary>Wählt die Art des Overlay-Hintergrunds anhand ihres Werts (UI/Test-Hilfe).</summary>
    /// <param name="background">Zu wählende Art.</param>
    public void SelectOverlayBackground(OverlayBackground background)
        => SelectedOverlayBackgroundOption = OverlayBackgrounds.First(option => option.Value == background);

    /// <summary>
    /// Übernimmt eine im Dateidialog gewählte Bilddatei und stellt den Hintergrund auf „Bild“.
    /// </summary>
    /// <param name="path">Vollständiger Pfad der gewählten Datei.</param>
    /// <returns>
    /// <c>false</c>, wenn die Datei kein unterstütztes Format hat; dann bleibt alles, wie es war.
    /// Der Dialog filtert bereits, die Prüfung hält aber auch einen anders gewählten Pfad fern.
    /// </returns>
    public bool UseOverlayImage(string path)
    {
        if (!OverlayImage.IsSupported(path))
        {
            return false;
        }

        BreakOverlayImagePath = path;
        SelectOverlayBackground(OverlayBackground.Image);
        return true;
    }

    /// <summary>
    /// Nimmt einen Fehler des Dateidialogs für das Overlay-Bild entgegen und protokolliert ihn; die
    /// Bildwahl bleibt, wie sie war.
    /// </summary>
    /// <param name="exception">Der Fehler aus dem Dialog.</param>
    public void ReportOverlayImagePickFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        SettingsViewModelLog.OverlayImagePickFailed(_logger, exception);
    }

    private void LoadOverlaySettings(Settings settings)
    {
        // Overlay-Farbe deckend übernehmen: WinUI 3 kann einen Alpha-Anteil nicht
        // als Durchsicht umsetzen. Eine gespeicherte halbtransparente Farbe wird
        // deshalb auf ihr sichtbares, opakes Äquivalent umgerechnet, damit sich das
        // Erscheinungsbild nicht ändert.
        BreakOverlayColor = HexColor.FlattenOverWhite(settings.BreakOverlayColor);
        SelectOverlayBackground(settings.BreakOverlayBackground);
        BreakOverlayImagePath = settings.BreakOverlayImagePath;
        BreakOverlayImageDimPercent = settings.BreakOverlayImageDimPercent;
    }

    private void StoreOverlaySettings(Settings settings)
    {
        settings.BreakOverlayColor = BreakOverlayColor;
        settings.BreakOverlayBackground = SelectedOverlayBackgroundOption?.Value ?? OverlayBackground.Color;
        settings.BreakOverlayImagePath = BreakOverlayImagePath;
        settings.BreakOverlayImageDimPercent = Math.Clamp(BreakOverlayImageDimPercent, OverlayImageDimMin, OverlayImageDimMax);
    }

    partial void OnBreakOverlayColorChanged(string value) => NotifyPersistCommands();
}
