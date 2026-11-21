using LookAway.Core.ValueObjects;

namespace LookAway.App.Services;

/// <summary>
/// Die Stellen, an denen der Update-Ablauf Prozesse und die laufende Installation berührt:
/// Programmordner und eigene Prozesskennung lesen, Helfer, Anwendung und Browser starten, auf das
/// Ende der alten Instanz warten und den Helfer beenden.
/// </summary>
/// <remarks>
/// Hinter dieser Schnittstelle liegt nur noch, was echte Prozesse startet oder beendet. Welche
/// dieser Aktionen der <see cref="UpdateOrchestrator"/> wann auslöst, lässt sich damit ohne
/// Prozessstart prüfen.
/// </remarks>
internal interface IUpdateProcess
{
    /// <summary>Programmordner der laufenden Installation.</summary>
    string InstallDirectory { get; }

    /// <summary>
    /// Ist diese Kopie über das Setup installiert? Sonst ist sie portabel (<c>portable.flag</c>
    /// neben der Programmdatei).
    /// </summary>
    bool IsSetupInstallation { get; }

    /// <summary>
    /// Startet ein Setup still. Es fragt selbst nach Administratorrechten, beendet die laufende
    /// Instanz und startet LookAway danach mit den Rechten des Benutzers neu.
    /// </summary>
    /// <param name="setupPath">Pfad des geprüften Setups.</param>
    void StartSetup(string setupPath);

    /// <summary>Kennung des laufenden Prozesses.</summary>
    int CurrentProcessId { get; }

    /// <summary>
    /// Startet die gestagte EXE im Helfer-Modus, damit sie nach dem Beenden dieser Instanz die
    /// Dateien im Zielordner ersetzt.
    /// </summary>
    /// <param name="stagedExePath">Pfad der EXE im Staging-Ordner.</param>
    /// <param name="args">Helfer-Parameter (PID, Quelle, Ziel).</param>
    void StartApply(string stagedExePath, UpdateApplyArgs args);

    /// <summary>Startet die installierte Anwendung normal.</summary>
    /// <param name="exePath">Pfad der installierten EXE.</param>
    void StartApp(string exePath);

    /// <summary>Öffnet eine Adresse im Standardbrowser.</summary>
    /// <param name="address">Bereits geprüfte http- oder https-Adresse.</param>
    void OpenInBrowser(Uri address);

    /// <summary>Wartet höchstens <paramref name="timeout"/>, bis der Prozess beendet ist.</summary>
    /// <param name="processId">Kennung des Prozesses; ein schon beendeter zählt als beendet.</param>
    /// <param name="timeout">Längste Wartezeit.</param>
    void WaitForExit(int processId, TimeSpan timeout);

    /// <summary>Beendet den laufenden Prozess sofort.</summary>
    void Exit();
}
