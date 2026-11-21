using LookAway.Core.Domain;
using LookAway.Core.Enums;

namespace LookAway.Core.Entities;

/// <summary>
/// Aussehen des Pausen-Overlays: Farbe oder eigenes Bild, und wie stark ein Bild abgedunkelt wird.
/// </summary>
public sealed partial class Settings
{
    /// <summary>
    /// Standardfarbe des Pausen-Overlays im ARGB-Hex-Format (<see cref="HexColor.Default"/>).
    /// </summary>
    public const string DefaultBreakOverlayColor = HexColor.Default;

    private string _breakOverlayColor = DefaultBreakOverlayColor;

    /// <summary>Untergrenze der Abdunklung über einem Overlay-Bild in Prozent.</summary>
    public const int MinOverlayImageDimPercent = 0;

    /// <summary>
    /// Obergrenze der Abdunklung über einem Overlay-Bild in Prozent. Darüber bliebe vom Bild kaum
    /// mehr als eine dunkle Fläche, und dafür gibt es die Farbe.
    /// </summary>
    public const int MaxOverlayImageDimPercent = 80;

    /// <summary>
    /// Vorgabe der Abdunklung. Bei 40 % bleibt ein Foto gut erkennbar, und helle Schrift liest
    /// sich auch auf einem hellen Motiv noch.
    /// </summary>
    public const int DefaultOverlayImageDimPercent = 40;

    private OverlayBackground _breakOverlayBackground = OverlayBackground.Color;
    private int _breakOverlayImageDimPercent = DefaultOverlayImageDimPercent;

    /// <summary>
    /// Hintergrundfarbe des Pausen-Overlays als Hex-Zeichenkette im Format
    /// <c>#RRGGBB</c> oder <c>#AARRGGBB</c>. Das Overlay ist immer deckend; eine
    /// Alpha-Angabe wird über Weiß eingerechnet (<see cref="HexColor.FlattenOverWhite(string)"/>)
    /// und hellt die Farbe nur auf. Default: <see cref="DefaultBreakOverlayColor"/>.
    /// </summary>
    public string BreakOverlayColor
    {
        get => _breakOverlayColor;
        set
        {
            if (!HexColor.IsValid(value))
            {
                throw new ArgumentException(
                    "BreakOverlayColor muss im Format #RRGGBB oder #AARRGGBB vorliegen.",
                    nameof(value));
            }
            _breakOverlayColor = value;
        }
    }

    /// <summary>
    /// Womit das Overlay den Bildschirm überdeckt. Default: <see cref="OverlayBackground.Color"/>.
    /// </summary>
    public OverlayBackground BreakOverlayBackground
    {
        get => _breakOverlayBackground;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unbekannte Art des Overlay-Hintergrunds.");
            }
            _breakOverlayBackground = value;
        }
    }

    /// <summary>
    /// Vollständiger Pfad des gewählten Overlay-Bilds, oder <c>null</c>, solange keines gewählt ist.
    /// </summary>
    /// <remarks>
    /// Bewusst ohne Prüfung beim Setzen: Die Datei kann zwischen Speichern und Pause verschwinden.
    /// Ob sie lesbar ist, entscheidet sich erst beim Zeigen; dann fällt das Overlay auf die Farbe
    /// zurück.
    /// </remarks>
    public string? BreakOverlayImagePath { get; set; }

    /// <summary>
    /// Wie stark ein Overlay-Bild abgedunkelt wird, in Prozent
    /// (<see cref="MinOverlayImageDimPercent"/> bis <see cref="MaxOverlayImageDimPercent"/>).
    /// Default: <see cref="DefaultOverlayImageDimPercent"/>.
    /// </summary>
    public int BreakOverlayImageDimPercent
    {
        get => _breakOverlayImageDimPercent;
        set
        {
            if (value is < MinOverlayImageDimPercent or > MaxOverlayImageDimPercent)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"BreakOverlayImageDimPercent muss zwischen {MinOverlayImageDimPercent} und {MaxOverlayImageDimPercent} liegen.");
            }
            _breakOverlayImageDimPercent = value;
        }
    }
}
