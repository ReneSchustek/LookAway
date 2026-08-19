using System.Globalization;

namespace LookAway.Core.Domain;

/// <summary>
/// Reine Hilfslogik für ARGB-Hex-Farben im Format <c>#RRGGBB</c> oder
/// <c>#AARRGGBB</c>. Ohne UI-/Plattformabhängigkeit, damit Validierung, Parsing
/// und Formatierung in allen Schichten einheitlich und testbar verwendet werden.
/// </summary>
public static class HexColor
{
    /// <summary>Dunkles, leicht transparentes Standard-Overlay (#F20F1115).</summary>
    public const string Default = "#F20F1115";

    /// <summary>Standard-ARGB-Komponenten, falls eine Eingabe ungültig ist.</summary>
    public static readonly (byte A, byte R, byte G, byte B) DefaultComponents = (0xF2, 0x0F, 0x11, 0x15);

    /// <summary>
    /// Prüft, ob die Zeichenkette eine gültige Hex-Farbe (<c>#RRGGBB</c> oder
    /// <c>#AARRGGBB</c>) ist.
    /// </summary>
    /// <param name="value">Zu prüfende Zeichenkette.</param>
    /// <returns><c>true</c>, wenn das Format gültig ist.</returns>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '#' || value.Length is not (7 or 9))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Versucht, eine Hex-Farbe in ihre ARGB-Komponenten zu zerlegen. Bei einem
    /// 6-stelligen Wert wird der Alpha-Kanal auf <c>0xFF</c> (deckend) gesetzt.
    /// </summary>
    /// <param name="value">Hex-Zeichenkette.</param>
    /// <param name="components">Ergebnis bei Erfolg.</param>
    /// <returns><c>true</c> bei gültiger Eingabe.</returns>
    public static bool TryParse(string? value, out (byte A, byte R, byte G, byte B) components)
    {
        components = default;
        if (!IsValid(value))
        {
            return false;
        }

        string body = value![1..];
        uint raw = uint.Parse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte a = body.Length == 8 ? (byte)((raw >> 24) & 0xFF) : (byte)0xFF;
        byte r = (byte)((raw >> 16) & 0xFF);
        byte g = (byte)((raw >> 8) & 0xFF);
        byte b = (byte)(raw & 0xFF);
        components = (a, r, g, b);
        return true;
    }

    /// <summary>
    /// Zerlegt eine Hex-Farbe in ARGB-Komponenten; bei ungültiger Eingabe werden
    /// die <see cref="DefaultComponents"/> zurückgegeben.
    /// </summary>
    /// <param name="value">Hex-Zeichenkette.</param>
    /// <returns>ARGB-Komponenten.</returns>
    public static (byte A, byte R, byte G, byte B) ParseOrDefault(string? value)
        => TryParse(value, out (byte A, byte R, byte G, byte B) components) ? components : DefaultComponents;

    /// <summary>Formatiert ARGB-Komponenten als <c>#AARRGGBB</c>.</summary>
    /// <param name="a">Alpha.</param>
    /// <param name="r">Rot.</param>
    /// <param name="g">Grün.</param>
    /// <param name="b">Blau.</param>
    /// <returns>Hex-Zeichenkette.</returns>
    public static string ToHex(byte a, byte r, byte g, byte b)
        => string.Create(CultureInfo.InvariantCulture, $"#{a:X2}{r:X2}{g:X2}{b:X2}");

    /// <summary>
    /// Setzt eine (evtl. halbtransparente) Farbe deckend über Weiß zusammen und gibt
    /// die resultierende <em>opake</em> Farbe (<c>#FFRRGGBB</c>) zurück. So bleibt das
    /// sichtbare Erscheinungsbild eines über hellem Grund gezeichneten Overlays
    /// erhalten, ohne einen Alphakanal zu benötigen. Bei bereits deckenden Farben
    /// (Alpha <c>0xFF</c>) unverändert.
    /// </summary>
    /// <param name="value">Hex-Farbe (<c>#RRGGBB</c> oder <c>#AARRGGBB</c>).</param>
    /// <returns>Deckende Farbe als <c>#FFRRGGBB</c>.</returns>
    public static string FlattenOverWhite(string? value)
    {
        (byte r, byte g, byte b) = FlattenOverWhite(ParseOrDefault(value));
        return ToHex(0xFF, r, g, b);
    }

    /// <summary>
    /// Setzt eine (evtl. halbtransparente) Farbe deckend über Weiß zusammen und gibt die
    /// sichtbaren Farbkanäle zurück. Bei bereits deckenden Farben unverändert.
    /// </summary>
    /// <param name="color">ARGB-Komponenten der Overlay-Farbe.</param>
    /// <returns>Die sichtbaren, deckenden Farbkanäle.</returns>
    /// <remarks>
    /// Weiß ist hier keine Annahme über den Bildschirminhalt, sondern der festgelegte
    /// Untergrund: Das Overlay-Fenster ist nicht durchsichtig, eine Alpha-Angabe kann also
    /// gar nichts durchscheinen lassen. Sie wird deshalb an dieser einen Stelle in die
    /// Farbe hineingerechnet — dieselbe Rechnung, mit der die Einstellungen eine
    /// halbtransparente Altfarbe auf ihr sichtbares Gegenstück umstellen.
    /// </remarks>
    public static (byte R, byte G, byte B) FlattenOverWhite((byte A, byte R, byte G, byte B) color)
    {
        byte Over(byte channel) => (byte)(((channel * color.A) + (255 * (255 - color.A))) / 255);
        return (Over(color.R), Over(color.G), Over(color.B));
    }

    /// <summary>
    /// Berechnet das Kontrastverhältnis zweier deckender Farben nach WCAG 2.1 — von
    /// <c>1</c> (nicht unterscheidbar) bis <c>21</c> (Schwarz auf Weiß).
    /// </summary>
    /// <param name="first">Erste Farbe.</param>
    /// <param name="second">Zweite Farbe.</param>
    /// <returns>Kontrastverhältnis, unabhängig von der Reihenfolge der Farben.</returns>
    /// <remarks>
    /// Ersetzt die frühere Hell/Dunkel-Schwelle. Eine Schwelle beantwortet die Frage nur
    /// mittelbar und lag am Ende falsch: Auf dem mittleren Grau, das eine halbtransparente
    /// Farbe ergibt, liest sich dunkler Text mit rund 6:1, heller mit rund 2,7:1 — eine
    /// Schwelle auf halber Luminanz hätte dort den helleren gewählt. Der Vergleich zweier
    /// Verhältnisse braucht keinen gesetzten Grenzwert und ist deshalb auch nicht daneben
    /// zu justieren.
    /// </remarks>
    public static double ContrastRatio((byte R, byte G, byte B) first, (byte R, byte G, byte B) second)
    {
        double a = RelativeLuminance(first.R, first.G, first.B);
        double b = RelativeLuminance(second.R, second.G, second.B);
        (double lighter, double darker) = a >= b ? (a, b) : (b, a);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(byte r, byte g, byte b)
        => (0.2126 * Linearize(r)) + (0.7152 * Linearize(g)) + (0.0722 * Linearize(b));

    private static double Linearize(byte channel)
    {
        double c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
