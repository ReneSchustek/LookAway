namespace LookAway.Core.Interfaces;

/// <summary>
/// Erkennt, ob im Vordergrund eine Vollbild-Anwendung laeuft (Praesentation,
/// Spiel, Video). Implementierungen kapseln den Plattform-Zugriff (Win32
/// <c>GetForegroundWindow</c> + Monitorvergleich).
/// </summary>
public interface IFullscreenDetector
{
    /// <summary>
    /// Prueft, ob das Vordergrundfenster den gesamten Monitor ausfuellt und
    /// nicht die Shell (Desktop/Sperrbildschirm) ist.
    /// </summary>
    /// <returns><c>true</c>, wenn eine Vollbild-Anwendung aktiv ist.</returns>
    bool IsFullscreenApplicationActive();
}
