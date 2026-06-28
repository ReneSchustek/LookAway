namespace LookAway.Core.Enums;

/// <summary>
/// Auswaehlbarer Erinnerungston. Die konkreten Audiodateien liefert
/// die Implementierung von <see cref="LookAway.Core.Interfaces.ISoundService"/>.
/// </summary>
public enum SoundType
{
    /// <summary>Sanftes hohes Gloeckchen.</summary>
    Chime,

    /// <summary>Warmer Glockenton.</summary>
    Bell,

    /// <summary>Kurzer, tiefer Plopp.</summary>
    Pop,
}
