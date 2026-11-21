using LookAway.Core.Entities;
using LookAway.Core.Enums;

namespace LookAway.Core.ValueObjects;

/// <summary>
/// Was das Pausen-Overlay zeigen soll: die Farbe und, falls gewählt, ein Bild mit seiner
/// Abdunklung.
/// </summary>
/// <param name="ColorHex">
/// Overlay-Farbe als <c>#RRGGBB</c>/<c>#AARRGGBB</c>. Sie gilt auch mit Bild: als Rückfall, wenn
/// das Bild nicht lesbar ist.
/// </param>
/// <param name="ImagePath">Pfad des Bilds, oder <c>null</c> für ein reines Farb-Overlay.</param>
/// <param name="ImageDimPercent">Abdunklung über dem Bild in Prozent.</param>
public sealed record OverlayBackdrop(string ColorHex, string? ImagePath, int ImageDimPercent)
{
    /// <summary>Soll ein Bild gezeigt werden?</summary>
    public bool HasImage => ImagePath is not null;

    /// <summary>Liest das Aussehen des Overlays aus den Einstellungen.</summary>
    /// <param name="settings">Aktuelle Einstellungen.</param>
    /// <returns>Das Aussehen des Overlays.</returns>
    /// <remarks>
    /// „Bild“ ohne gewählte Datei ist ein Zwischenstand der Einstellungen, kein Fehler: Das
    /// Overlay zeigt dann still die Farbe, statt bei jeder Pause einen Fehlschlag zu protokollieren.
    /// </remarks>
    public static OverlayBackdrop From(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? imagePath = settings.BreakOverlayBackground == OverlayBackground.Image
            && !string.IsNullOrWhiteSpace(settings.BreakOverlayImagePath)
                ? settings.BreakOverlayImagePath
                : null;

        return new OverlayBackdrop(settings.BreakOverlayColor, imagePath, settings.BreakOverlayImageDimPercent);
    }
}
