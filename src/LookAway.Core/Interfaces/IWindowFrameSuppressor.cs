namespace LookAway.Core.Interfaces;

/// <summary>
/// Nimmt einem Fenster den Schmuck, den die Fensterverwaltung des Systems von sich aus
/// zeichnet — Randlinie und abgerundete Ecken. UI- und plattformfreie Sicht darauf: Ein
/// bildschirmfüllendes Overlay deckt seinen Monitor erst damit bis zum äußersten Bildpunkt ab.
/// </summary>
public interface IWindowFrameSuppressor
{
    /// <summary>
    /// Unterdrückt Randlinie und Eckenrundung des Fensters. Mehrfaches Aufrufen ist
    /// unschädlich.
    /// </summary>
    /// <param name="windowHandle">
    /// Fenster-Kennung der Plattform (unter Windows das Fenster-Handle). Eine leere
    /// Kennung wird ignoriert.
    /// </param>
    /// <remarks>
    /// Kennt das System die nötigen Einstellungen nicht, bleibt der Aufruf wirkungslos und
    /// der Schmuck stehen. Das ist kein Fehlerfall: Die Anwendung bleibt bedienbar, das
    /// Overlay deckt dort nur nicht ganz bis an den Rand.
    /// </remarks>
    void SuppressFrame(nint windowHandle);
}
