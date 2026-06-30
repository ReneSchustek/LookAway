namespace LookAway.Core.Enums;

/// <summary>
/// Auswählbarer Erinnerungston. Die konkreten Audiodateien liefert
/// die Implementierung von <see cref="LookAway.Core.Interfaces.ISoundService"/>.
/// </summary>
public enum SoundType
{
    /// <summary>Sanftes hohes Glöckchen.</summary>
    Chime,

    /// <summary>Warmer Glockenton.</summary>
    Bell,

    /// <summary>Kurzer, tiefer Plopp.</summary>
    Pop,
}
