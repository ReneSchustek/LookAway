using LookAway.Core.Enums;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Spielt dezente Erinnerungstoene ab. Die Lautstaerke wird pro
/// Wiedergabe gesetzt und veraendert die System-Lautstaerke nicht.
/// </summary>
public interface ISoundService
{
    /// <summary>
    /// Spielt den angegebenen Ton mit der gewuenschten Lautstaerke. Eine bereits
    /// laufende Wiedergabe wird zuvor gestoppt (keine Ueberlappung). Fehler
    /// (z. B. fehlendes Audiogeraet) werden geschluckt — die App soll dadurch
    /// nie abstuerzen.
    /// </summary>
    /// <param name="soundType">Abzuspielender Ton.</param>
    /// <param name="volumePercent">Lautstaerke in Prozent (0–100).</param>
    void Play(SoundType soundType, int volumePercent);
}
