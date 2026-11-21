using LookAway.Core.Domain;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Services;

/// <summary>
/// Was die Overlay-Fenster zeichnen: eine deckende Farbe und, falls ein lesbares Bild gewählt ist,
/// das Bild mit seiner Abdunklung darüber.
/// </summary>
/// <param name="Color">
/// Deckende Overlay-Farbe. Mit Bild liegt sie darunter und füllt den Monitor, bis das Bild
/// gezeichnet ist, und auch dann, wenn es sich doch nicht laden lässt.
/// </param>
/// <param name="Image">Das Bild, oder <c>null</c> für ein reines Farb-Overlay.</param>
internal sealed record OverlayLook((byte R, byte G, byte B) Color, OverlayImageLayer? Image)
{
    /// <summary>
    /// Die Fläche, gegen die die Schriftfarbe gewählt wird: mit Bild dessen abgedunkelte mittlere
    /// Farbe, sonst die Overlay-Farbe.
    /// </summary>
    public (byte R, byte G, byte B) TextSurface => Image?.TextSurface ?? Color;

    /// <summary>Ein reines Farb-Overlay.</summary>
    /// <param name="color">Deckende Overlay-Farbe.</param>
    /// <returns>Das Aussehen ohne Bild.</returns>
    public static OverlayLook ForColor((byte R, byte G, byte B) color) => new(color, null);

    /// <summary>Ein Overlay mit Bild über der Farbe.</summary>
    /// <param name="color">Deckende Overlay-Farbe darunter.</param>
    /// <param name="path">Pfad des Bilds.</param>
    /// <param name="info">Größe und mittlere Farbe des gelesenen Bilds.</param>
    /// <param name="dimPercent">Abdunklung über dem Bild in Prozent.</param>
    /// <returns>Das Aussehen mit Bild.</returns>
    public static OverlayLook WithImage((byte R, byte G, byte B) color, string path, OverlayImageInfo info, int dimPercent)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(info);

        return new OverlayLook(color, new OverlayImageLayer(
            path,
            info.PixelWidth,
            info.PixelHeight,
            dimPercent / 100.0,
            OverlayImage.Dim(info.AverageColor, dimPercent)));
    }
}

/// <summary>
/// Das Bild eines Overlays, wie die Fenster es zeichnen.
/// </summary>
/// <param name="Path">Pfad der Bilddatei.</param>
/// <param name="PixelWidth">Breite des Bilds; zusammen mit der Höhe bestimmt sie die Dekodiergröße je Monitor.</param>
/// <param name="PixelHeight">Höhe des Bilds.</param>
/// <param name="DimOpacity">Deckkraft der schwarzen Abdunklung über dem Bild (0 bis 1).</param>
/// <param name="TextSurface">Abgedunkelte mittlere Bildfarbe, gegen die die Schriftfarbe gewählt wird.</param>
internal sealed record OverlayImageLayer(string Path, int PixelWidth, int PixelHeight, double DimOpacity, (byte R, byte G, byte B) TextSurface);
