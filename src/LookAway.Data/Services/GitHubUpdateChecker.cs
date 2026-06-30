using System.Text.Json;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Services;

/// <summary>
/// Prüft die neueste Release über die GitHub-Releases-API. Der
/// eigentliche Netzwerkzugriff ist über <see cref="IHttpGetClient"/> gekapselt
/// und damit testbar; die Versionsvergleichslogik liegt in <see cref="UpdateInfo"/>.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly Uri ReleasesApiUri =
        new("https://api.github.com/repos/ReneSchustek/LookAway/releases/latest");

    private readonly IHttpGetClient _httpClient;
    private readonly Version _currentVersion;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    /// <summary>
    /// Erzeugt den Update-Checker.
    /// </summary>
    /// <param name="httpClient">Lesender HTTP-Zugriff.</param>
    /// <param name="currentVersion">Installierte Version.</param>
    /// <param name="logger">Logger.</param>
    public GitHubUpdateChecker(IHttpGetClient httpClient, Version currentVersion, ILogger<GitHubUpdateChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(currentVersion);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _currentVersion = currentVersion;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        string? json = await _httpClient.GetStringAsync(ReleasesApiUri, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return UpdateInfo.NoUpdate(_currentVersion);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string? tag = GetString(root, "tag_name");
            string? htmlUrl = GetString(root, "html_url");
            string? body = GetString(root, "body");
            string? packageUrl = FindPackageAssetUrl(root);
            string? signatureUrl = FindSignatureAssetUrl(root);

            UpdateInfo info = UpdateInfo.Create(_currentVersion, tag, htmlUrl, body, packageUrl, signatureUrl);
            if (info.IsUpdateAvailable)
            {
                GitHubUpdateCheckerLog.UpdateAvailable(_logger, info.LatestVersion);
            }

            return info;
        }
        catch (JsonException ex)
        {
            GitHubUpdateCheckerLog.ResponseUnparsable(_logger, ex);
            return UpdateInfo.NoUpdate(_currentVersion);
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Sucht in den Release-Assets die Portable-ZIP und liefert deren direkte
    /// Download-URL. Bevorzugt einen Namen mit "portable"; fällt sonst auf das
    /// erste <c>.zip</c>-Asset zurück. Akzeptiert nur vertrauenswürdige
    /// GitHub-HTTPS-URLs, damit kein Downgrade-/Fremdhost-Asset eingespielt wird.
    /// </summary>
    private static string? FindPackageAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? firstZip = null;
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = GetString(asset, "name");
            string? url = GetString(asset, "browser_download_url");
            if (name is null
                || url is null
                || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || !IsTrustedGitHubUrl(url))
            {
                continue;
            }

            firstZip ??= url;
            if (name.Contains("portable", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
        }

        return firstZip;
    }

    /// <summary>
    /// Sucht in den Release-Assets die losgelöste Signatur (Name endet auf
    /// <c>.sig</c>) und liefert deren direkte Download-URL. Akzeptiert nur
    /// vertrauenswürdige GitHub-HTTPS-URLs.
    /// </summary>
    private static string? FindSignatureAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = GetString(asset, "name");
            string? url = GetString(asset, "browser_download_url");
            if (name is not null
                && url is not null
                && name.EndsWith(".sig", StringComparison.OrdinalIgnoreCase)
                && IsTrustedGitHubUrl(url))
            {
                return url;
            }
        }

        return null;
    }

    /// <summary>
    /// Prüft, ob eine URL über HTTPS auf einen GitHub-Releasehost zeigt
    /// (<c>github.com</c> oder <c>*.githubusercontent.com</c>).
    /// </summary>
    private static bool IsTrustedGitHubUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string host = uri.Host;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Update-Checkers.
/// </summary>
internal static partial class GitHubUpdateCheckerLog
{
    [LoggerMessage(EventId = 1600, Level = LogLevel.Information, Message = "Update verfügbar: Version {Version}.")]
    public static partial void UpdateAvailable(ILogger logger, string version);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Warning, Message = "Antwort der Update-Prüfung konnte nicht ausgewertet werden.")]
    public static partial void ResponseUnparsable(ILogger logger, Exception exception);
}
