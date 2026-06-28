namespace LookAway.Core.ValueObjects;

/// <summary>
/// Ergebnis einer Update-Pruefung (BRIEF020). Enthaelt die reine, testbare
/// Vergleichslogik zwischen installierter und neuester Version.
/// </summary>
public sealed class UpdateInfo
{
    private UpdateInfo(bool isUpdateAvailable, string latestVersion, Uri? downloadUrl, string releaseNotes)
    {
        IsUpdateAvailable = isUpdateAvailable;
        LatestVersion = latestVersion;
        DownloadUrl = downloadUrl;
        ReleaseNotes = releaseNotes;
    }

    /// <summary>Wahr, wenn eine neuere Version verfuegbar ist.</summary>
    public bool IsUpdateAvailable { get; }

    /// <summary>Die neueste bekannte Version (oder die installierte, wenn keine ermittelt wurde).</summary>
    public string LatestVersion { get; }

    /// <summary>URL zur Release-Seite, falls vorhanden.</summary>
    public Uri? DownloadUrl { get; }

    /// <summary>Release-Notes (Plaintext/Markdown), ggf. leer.</summary>
    public string ReleaseNotes { get; }

    /// <summary>Ergebnis "kein Update verfuegbar".</summary>
    /// <param name="current">Installierte Version.</param>
    public static UpdateInfo NoUpdate(Version current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return new UpdateInfo(false, current.ToString(), downloadUrl: null, releaseNotes: string.Empty);
    }

    /// <summary>
    /// Erzeugt das Ergebnis aus den Daten der neuesten GitHub-Release.
    /// </summary>
    /// <param name="current">Installierte Version.</param>
    /// <param name="tagName">Tag der Release (z. B. <c>"v1.2.0"</c>).</param>
    /// <param name="htmlAddress">Adresse der Release-Seite (Roh-String aus der API).</param>
    /// <param name="releaseNotes">Release-Notes.</param>
    /// <returns>
    /// Ein <see cref="UpdateInfo"/>; <see cref="IsUpdateAvailable"/> ist nur dann
    /// <c>true</c>, wenn das Tag eine hoehere Version als <paramref name="current"/> ergibt.
    /// </returns>
    public static UpdateInfo Create(Version current, string? tagName, string? htmlAddress, string? releaseNotes)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (!TryParseTag(tagName, out Version? latest) || latest is null)
        {
            return NoUpdate(current);
        }

        Uri? downloadUrl = Uri.TryCreate(htmlAddress, UriKind.Absolute, out Uri? uri) ? uri : null;
        bool isNewer = latest > current;

        return new UpdateInfo(
            isNewer,
            latest.ToString(),
            downloadUrl,
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

        // Nur den numerischen Versionsanteil beruecksichtigen (z. B. "1.2.0-beta" → "1.2.0").
        int dashIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            trimmed = trimmed[..dashIndex];
        }

        return Version.TryParse(trimmed, out version);
    }
}
