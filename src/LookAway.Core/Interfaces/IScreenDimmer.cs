namespace LookAway.Core.Interfaces;

/// <summary>
/// Steuert die Bildschirmhelligkeit waehrend einer Pause.
/// Implementierungen kapseln die Plattform-Anbindung (DDC/CI bzw. Fallback).
/// </summary>
public interface IScreenDimmer
{
    /// <summary>
    /// Dimmt den Bildschirm auf den Zielwert und merkt sich die vorherige
    /// Helligkeit. Fehler (kein DDC/CI-faehiger Monitor) werden geschluckt.
    /// </summary>
    /// <param name="targetPercent">Zielhelligkeit in Prozent (0–100).</param>
    void DimTo(int targetPercent);

    /// <summary>Stellt die vor dem Dimmen gemerkte Helligkeit wieder her.</summary>
    void Restore();
}
