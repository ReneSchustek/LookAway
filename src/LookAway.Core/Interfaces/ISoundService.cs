using LookAway.Core.Enums;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Spielt dezente Erinnerungstöne ab. Die Lautstärke wird pro
/// Wiedergabe gesetzt und verändert die System-Lautstärke nicht.
/// </summary>
public interface ISoundService
{
    /// <summary>
    /// Spielt den angegebenen Ton mit der gewünschten Lautstärke. Eine bereits
    /// laufende Wiedergabe wird zuvor gestoppt (keine Überlappung). Fehler
    /// (z. B. fehlendes Audiogerät) werden geschluckt — die App soll dadurch
    /// nie abstürzen.
    /// </summary>
    /// <param name="soundType">Abzuspielender Ton.</param>
    /// <param name="volumePercent">Lautstärke in Prozent (0–100).</param>
    void Play(SoundType soundType, int volumePercent);
}
