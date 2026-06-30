namespace LookAway.Core.Interfaces;

/// <summary>
/// Liest die Benutzer-Inaktivitätsdauer (Zeit seit der letzten Maus-/Tastatur-
/// Eingabe). Implementierungen kapseln den Plattform-Zugriff (Win32
/// <c>GetLastInputInfo</c>).
/// </summary>
public interface IIdleDetector
{
    /// <summary>
    /// Liefert die seit der letzten Eingabe verstrichene Zeit.
    /// </summary>
    /// <returns>Inaktivitätsdauer; <see cref="System.TimeSpan.Zero"/>, wenn unbestimmbar.</returns>
    TimeSpan GetIdleTime();
}
