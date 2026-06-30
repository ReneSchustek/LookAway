namespace LookAway.Core.Interfaces;

/// <summary>
/// Abstraktion über den benutzerspezifischen Windows-Autostart. Implementierungen
/// tragen die Anwendung so ein, dass sie beim Login des aktuellen Benutzers
/// automatisch gestartet wird — ohne Administrator-Rechte.
/// </summary>
/// <remarks>
/// Die Operationen sind bewusst synchron: der zugrundeliegende Mechanismus
/// (Windows-Registry, <c>HKEY_CURRENT_USER</c>) ist eine schnelle, lokale
/// Operation ohne IO-Wartezeiten. Fehler werden als
/// <see cref="LookAway.Core.Exceptions.AutoStartException"/> signalisiert, damit
/// Aufrufer sie gezielt behandeln können.
/// </remarks>
public interface IAutoStartService
{
    /// <summary>
    /// Prüft, ob der Autostart-Eintrag für den aktuellen Benutzer vorhanden ist.
    /// </summary>
    /// <returns><c>true</c>, wenn ein Eintrag existiert; sonst <c>false</c>.</returns>
    /// <exception cref="LookAway.Core.Exceptions.AutoStartException">
    /// Der Autostart-Status konnte nicht gelesen werden.
    /// </exception>
    bool IsEnabled();

    /// <summary>
    /// Trägt die Anwendung mit ihrem aktuellen Pfad in den Autostart ein.
    /// Idempotent: ein bereits korrekter Eintrag wird nicht erneut geschrieben;
    /// ein veralteter Pfad (Anwendung verschoben) wird korrigiert.
    /// </summary>
    /// <exception cref="LookAway.Core.Exceptions.AutoStartException">
    /// Der Autostart-Eintrag konnte nicht geschrieben werden.
    /// </exception>
    void Enable();

    /// <summary>
    /// Entfernt den Autostart-Eintrag des aktuellen Benutzers. Ein nicht
    /// vorhandener Eintrag ist kein Fehler.
    /// </summary>
    /// <exception cref="LookAway.Core.Exceptions.AutoStartException">
    /// Der Autostart-Eintrag konnte nicht entfernt werden.
    /// </exception>
    void Disable();
}
