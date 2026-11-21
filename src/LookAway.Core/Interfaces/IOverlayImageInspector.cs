using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Liest ein Overlay-Bild probeweise, bevor es gezeigt wird: Größe und mittlere Farbe.
/// </summary>
public interface IOverlayImageInspector
{
    /// <summary>Liest das Bild unter <paramref name="path"/>.</summary>
    /// <param name="path">Vollständiger Pfad der Bilddatei.</param>
    /// <param name="cancellationToken">Abbruch.</param>
    /// <returns>
    /// Größe und mittlere Farbe, oder <c>null</c>, wenn die Datei fehlt, nicht lesbar ist, kein
    /// unterstütztes Format hat oder sich nicht dekodieren lässt. Der Grund steht dann im Protokoll.
    /// </returns>
    Task<OverlayImageInfo?> InspectAsync(string path, CancellationToken cancellationToken = default);
}
