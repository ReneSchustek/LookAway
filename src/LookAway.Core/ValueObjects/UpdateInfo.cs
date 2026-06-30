namespace LookAway.Core.ValueObjects;

/// <summary>
/// Ergebnis einer Update-Prüfung. Enthält die reine, testbare
/// Vergleichslogik zwischen installierter und neuester Version.
/// </summary>
public sealed class UpdateInfo
{
    private UpdateInfo(bool isUpdateAvailable, string latestVersion, Uri? downloadUrl, Uri? packageUrl, Uri? signatureUrl, string releaseNotes)
    {
        IsUpdateAvailable = isUpdateAvailable;
        LatestVersion = latestVersion;
        DownloadUrl = downloadUrl;
        PackageUrl = packageUrl;
        SignatureUrl = signatureUrl;
        ReleaseNotes = releaseNotes;
    }

    /// <summary>Wahr, wenn eine neuere Version verfügbar ist.</summary>
    public bool IsUpdateAvailable { get; }

    /// <summary>Die neueste bekannte Version (oder die installierte, wenn keine ermittelt wurde).</summary>
    public string LatestVersion { get; }

    /// <summary>URL zur Release-Seite, falls vorhanden.</summary>
    public Uri? DownloadUrl { get; }

    /// <summary>
    /// Direkte Download-URL des Installationspakets (Portable-ZIP) aus den
    /// Release-Assets, falls vorhanden. Grundlage für die automatische Aktualisierung.
    /// </summary>
    public Uri? PackageUrl { get; }

    /// <summary>
    /// Direkte Download-URL der losgelösten Signatur (<c>&lt;paket&gt;.sig</c>) des
    /// Pakets, falls vorhanden. Wird vor dem Einspielen gegen den eingebetteten
    /// öffentlichen Schlüssel geprüft.
    /// </summary>
    public Uri? SignatureUrl { get; }

    /// <summary>Release-Notes (Plaintext/Markdown), ggf. leer.</summary>
    public string ReleaseNotes { get; }

    /// <summary>Ergebnis "kein Update verfügbar".</summary>
    /// <param name="current">Installierte Version.</param>
    public static UpdateInfo NoUpdate(Version current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return new UpdateInfo(false, current.ToString(), downloadUrl: null, packageUrl: null, signatureUrl: null, releaseNotes: string.Empty);
    }

    /// <summary>
    /// Erzeugt das Ergebnis aus den Daten der neuesten GitHub-Release.
    /// </summary>
    /// <param name="current">Installierte Version.</param>
    /// <param name="tagName">Tag der Release (z. B. <c>"v1.2.0"</c>).</param>
    /// <param name="htmlAddress">Adresse der Release-Seite (Roh-String aus der API).</param>
    /// <param name="releaseNotes">Release-Notes.</param>
    /// <param name="packageAddress">Direkte Download-URL des Installationspakets (Portable-ZIP), optional.</param>
    /// <param name="signatureAddress">Direkte Download-URL der losgelösten Paket-Signatur, optional.</param>
    /// <returns>
    /// Ein <see cref="UpdateInfo"/>; <see cref="IsUpdateAvailable"/> ist nur dann
    /// <c>true</c>, wenn das Tag eine höhere Version als <paramref name="current"/> ergibt.
    /// </returns>
    public static UpdateInfo Create(Version current, string? tagName, string? htmlAddress, string? releaseNotes, string? packageAddress = null, string? signatureAddress = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (!TryParseTag(tagName, out Version? latest) || latest is null)
        {
            return NoUpdate(current);
        }

        Uri? downloadUrl = Uri.TryCreate(htmlAddress, UriKind.Absolute, out Uri? uri) ? uri : null;
        Uri? packageUrl = Uri.TryCreate(packageAddress, UriKind.Absolute, out Uri? pkg) ? pkg : null;
        Uri? signatureUrl = Uri.TryCreate(signatureAddress, UriKind.Absolute, out Uri? sig) ? sig : null;
        bool isNewer = latest > current;

        return new UpdateInfo(
            isNewer,
            latest.ToString(),
            downloadUrl,
            packageUrl,
            signatureUrl,
            releaseNotes ?? string.Empty);
    }

    private static bool TryParseTag(string? tagName, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        string trimmed = tagName.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        // Nur den numerischen Versionsanteil berücksichtigen (z. B. "1.2.0-beta" → "1.2.0").
        int dashIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            trimmed = trimmed[..dashIndex];
        }

        return Version.TryParse(trimmed, out version);
    }
}
