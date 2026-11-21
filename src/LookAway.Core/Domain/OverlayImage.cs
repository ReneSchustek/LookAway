namespace LookAway.Core.Domain;

/// <summary>
/// Regeln für ein eigenes Overlay-Bild: welche Dateien taugen, wie die Abdunklung die Farbe
/// ändert, gegen die die Schrift gelesen wird, und wie groß das Bild für einen Monitor dekodiert
/// werden muss.
/// </summary>
public static class OverlayImage
{
    /// <summary>
    /// Dateiendungen, die das Overlay als Bild annimmt. JPG und PNG decken Fotos und Grafiken ab
    /// und liest jeder Windows-Bilddecoder ohne Zusatz.
    /// </summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } = [".jpg", ".jpeg", ".png"];

    /// <summary>Ob <paramref name="path"/> eine Datei in einem unterstützten Format benennt.</summary>
    /// <param name="path">Pfad oder Dateiname.</param>
    /// <returns><c>true</c> bei einer unterstützten Endung, unabhängig von Groß- und Kleinschreibung.</returns>
    public static bool IsSupported(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        return SupportedExtensions.Any(supported => string.Equals(supported, extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Die Farbe, die das Auge nach der Abdunklung im Mittel sieht: Schwarz mit
    /// <paramref name="dimPercent"/> Prozent Deckkraft über der mittleren Bildfarbe.
    /// </summary>
    /// <param name="averageColor">Mittlere Farbe des Bilds.</param>
    /// <param name="dimPercent">Stärke der Abdunklung in Prozent (0 bis 100).</param>
    /// <returns>Die abgedunkelte Farbe; gegen sie entscheidet sich die Schriftfarbe.</returns>
    /// <remarks>
    /// Ein Bild hat keine einzelne Farbe. Der Mittelwert ist die Fläche, auf der die Schrift im
    /// Schnitt steht; zusammen mit der Abdunklung hält das den Kontrast auch dort, wo das Motiv
    /// unter dem Text heller oder dunkler ist als im Mittel.
    /// </remarks>
    public static (byte R, byte G, byte B) Dim((byte R, byte G, byte B) averageColor, int dimPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dimPercent, 100);

        byte Darken(byte channel) => (byte)Math.Round(channel * (100 - dimPercent) / 100.0, MidpointRounding.AwayFromZero);
        return (Darken(averageColor.R), Darken(averageColor.G), Darken(averageColor.B));
    }

    /// <summary>
    /// Die Breite in Pixeln, auf die das Bild für einen Monitor dekodiert werden muss, damit es ihn
    /// füllend (Seitenverhältnis bleibt, Ränder werden beschnitten) ohne Unschärfe deckt.
    /// </summary>
    /// <param name="imageWidth">Breite des Bilds in Pixeln.</param>
    /// <param name="imageHeight">Höhe des Bilds in Pixeln.</param>
    /// <param name="areaWidth">Breite des Monitors in Pixeln.</param>
    /// <param name="areaHeight">Höhe des Monitors in Pixeln.</param>
    /// <returns>Dekodierbreite, höchstens die Bildbreite selbst.</returns>
    /// <remarks>
    /// Ein Foto mit 50 Megapixeln belegte voll dekodiert je Monitor rund 200 MB. Füllend zählt
    /// die Seite, die den Monitor zuerst erreicht; auf einem Hochkant-Monitor ist das bei einem
    /// Querformat die Höhe, und die Breite wird entsprechend größer.
    /// </remarks>
    public static int DecodeWidth(int imageWidth, int imageHeight, int areaWidth, int areaHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(areaWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(areaHeight);

        double scale = Math.Max((double)areaWidth / imageWidth, (double)areaHeight / imageHeight);
        return Math.Min(imageWidth, (int)Math.Ceiling(imageWidth * scale));
    }
}
