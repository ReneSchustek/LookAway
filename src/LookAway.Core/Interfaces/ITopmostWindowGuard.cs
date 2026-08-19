namespace LookAway.Core.Interfaces;

/// <summary>
/// Hält ein Fenster an der Spitze der obersten Fensterebene. UI- und plattformfreie
/// Sicht auf die Z-Reihenfolge: Das Pausen-Overlay bleibt damit auch dann vorn, wenn
/// sich ein fremdes Fenster von sich aus nach vorn schiebt.
/// </summary>
public interface ITopmostWindowGuard
{
    /// <summary>
    /// Hebt das Fenster über alle anderen, ohne ihm den Eingabefokus zu geben.
    /// Mehrfaches Aufrufen ist unschädlich.
    /// </summary>
    /// <param name="windowHandle">
    /// Fenster-Kennung der Plattform (unter Windows das Fenster-Handle). Eine leere
    /// Kennung wird ignoriert.
    /// </param>
    void BringToTop(nint windowHandle);
}
