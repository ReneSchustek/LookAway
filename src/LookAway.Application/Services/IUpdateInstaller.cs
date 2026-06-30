using LookAway.Core.ValueObjects;

namespace LookAway.Application.Services;

/// <summary>
/// Schmale Abstraktion über das Herunterladen und Bereitstellen (Staging) eines
/// Update-Pakets. Erlaubt es dem <see cref="ViewModels.SettingsViewModel"/>, ein
/// gefundenes Update direkt „mit einem Klick" vorzubereiten, ohne die volle
/// <see cref="UpdateInstallerService"/>-Implementierung (Datei-Tausch, Rollback,
/// Prozesssteuerung) zu kennen — und macht den Pfad in Tests ersetzbar.
/// </summary>
/// <remarks>
/// Das tatsächliche Einspielen (Datei-Tausch) geschieht bewusst erst beim nächsten
/// Programmstart über den Anwendungs-Bootstrap; hier wird das Paket nur geprüft
/// (inkl. Signatur) und in den Staging-Ordner entpackt.
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
}
