using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Ändert einen Wert in den gespeicherten Einstellungen, ohne den Aufrufer an einem Schreibfehler
/// scheitern zu lassen. Der Update-Ablauf vermerkt so Prüfzeitpunkt und ausstehendes Paket.
/// </summary>
internal static class SettingsWriter
{
    /// <summary>
    /// Lädt die Einstellungen, wendet die Änderung an und speichert sie. Ein Datei- oder
    /// Rechtefehler wird protokolliert; der Vermerk fehlt dann und wird beim nächsten Mal nachgeholt.
    /// </summary>
    /// <param name="repository">Ablage der Einstellungen.</param>
    /// <param name="change">Die Änderung am geladenen Stand.</param>
    /// <param name="logger">Protokolliert einen gescheiterten Vermerk.</param>
    /// <param name="cancellationToken">Abbruch beim Beenden der Anwendung.</param>
    /// <returns>Der Vorgang; er wirft nur bei Abbruch.</returns>
    public static async Task TryChangeAsync(
        ISettingsRepository repository,
        Action<Settings> change,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Settings current = await repository.LoadAsync(cancellationToken).ConfigureAwait(true);
            change(current);
            await repository.SaveAsync(current, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            UpdateOrchestratorLog.CheckPersistFailed(logger, ex);
        }
    }
}
