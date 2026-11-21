using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Schmale Abstraktion über das Herunterladen und Bereitstellen (Staging) eines
/// Update-Pakets. Erlaubt es der Oberfläche, ein gefundenes Update direkt „mit einem
/// Klick" vorzubereiten, ohne die volle Installer-Implementierung (Datei-Tausch,
/// Rollback, Prozesssteuerung) zu kennen — und macht den Pfad in Tests ersetzbar.
/// </summary>
/// <remarks>
/// Das tatsächliche Einspielen (Datei-Tausch) geschieht bewusst erst beim nächsten
/// Programmstart über den Anwendungs-Bootstrap. Bis dahin ist das Paket nur geprüft
/// (samt Signatur) und in den Staging-Ordner entpackt; das laufende Programm bleibt unberührt.
/// </remarks>
public interface IUpdateInstaller
{
    /// <summary>
    /// Lädt das Paket der Aktualisierung herunter, verifiziert seine Signatur und
    /// entpackt es in den versionsbenannten Staging-Ordner.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>
    /// Staging-Ordner, Version und Datei-Hash bei Erfolg; <see langword="null"/>,
    /// wenn Download, Signaturprüfung oder Entpacken fehlschlagen.
    /// </returns>
    Task<StagedUpdate?> DownloadAndStageAsync(UpdateInfo info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stellt das Setup der Aktualisierung bereit: lädt es samt Signatur, falls es noch nicht
    /// bereitliegt, sperrt die Datei gegen Schreiben und prüft die Signatur an ihr.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>
    /// Das gesperrte, geprüfte Setup, das der Aufrufer nach dem Start entsorgt; <see langword="null"/>,
    /// wenn das Release kein signiertes Setup hat oder Download oder Prüfung scheitern.
    /// </returns>
    Task<VerifiedSetup?> PrepareSetupAsync(UpdateInfo info, CancellationToken cancellationToken = default);
}
