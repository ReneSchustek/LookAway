namespace LookAway.Core.ValueObjects;

/// <summary>
/// Was das Overlay über ein gelesenes Bild wissen muss: seine Größe für die Dekodierung je Monitor
/// und seine mittlere Farbe für die Wahl der Schriftfarbe.
/// </summary>
/// <param name="PixelWidth">Breite in Pixeln.</param>
/// <param name="PixelHeight">Höhe in Pixeln.</param>
/// <param name="AverageColor">Mittlere Farbe über das ganze Bild.</param>
public sealed record OverlayImageInfo(int PixelWidth, int PixelHeight, (byte R, byte G, byte B) AverageColor);
