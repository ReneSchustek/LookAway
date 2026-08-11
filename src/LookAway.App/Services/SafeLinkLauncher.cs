using System.Diagnostics.CodeAnalysis;
using Windows.System;

namespace LookAway.App.Services;

/// <summary>
/// Öffnet Verweise im Browser — und ausschließlich solche.
/// </summary>
/// <remarks>
/// Ein Verweis wird nur gestartet, wenn er absolut ist und auf <c>http</c> oder
/// <c>https</c> lautet. Das klingt übervorsichtig für eine Adresse, die als Konstante
/// im Programm steht, ist es aber nicht: Die Dokumentations-Adresse daneben stammt aus
/// den Sprachdateien und ist damit eine Datei, die jemand austauschen kann. Ohne die
/// Prüfung würde an dieser Stelle starten, was dort drinsteht — auch ein Pfad auf eine
/// ausführbare Datei.
/// </remarks>
internal static class SafeLinkLauncher
{
    /// <summary>
    /// Prüft, ob ein Verweis geöffnet werden darf.
    /// </summary>
    /// <param name="uri">Zu prüfender Verweis; <c>null</c> ist zulässig und nie erlaubt.</param>
    /// <returns><c>true</c>, wenn er absolut ist und http oder https verwendet.</returns>
    /// <remarks>
    /// Die Zusicherung, dass der Verweis bei <c>true</c> nicht <c>null</c> ist, steht als
    /// Attribut da und nicht als Kommentar: So braucht der Aufrufer hinter der Prüfung
    /// keine zweite auf <c>null</c>, die nur den Übersetzer beruhigt.
    /// </remarks>
    public static bool IsAllowed([NotNullWhen(true)] Uri? uri)
        => uri is { IsAbsoluteUri: true }
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Öffnet einen erlaubten Verweis im Standardbrowser.
    /// </summary>
    /// <param name="uri">Zu öffnender Verweis.</param>
    /// <returns>
    /// <c>true</c>, wenn er geöffnet wurde; <c>false</c>, wenn er abgelehnt wurde oder
    /// das System keine Anwendung dafür kennt.
    /// </returns>
    public static async Task<bool> OpenAsync(Uri? uri)
    {
        if (!IsAllowed(uri))
        {
            return false;
        }

        return await Launcher.LaunchUriAsync(uri).AsTask().ConfigureAwait(true);
    }
}
