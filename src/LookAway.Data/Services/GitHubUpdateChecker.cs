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
            UpdateInfo info = ReadRelease(document.RootElement);
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

    // Liest Version, Release-Seite, Notizen und die Download-Adressen aus der API-Antwort.
    private UpdateInfo ReadRelease(JsonElement root)
        => UpdateInfo.Create(
            _currentVersion,
            GetString(root, "tag_name"),
            GetString(root, "html_url"),
            GetString(root, "body"),
            ReadAssets(root));

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Wählt aus den Release-Assets Portable-ZIP und Setup samt ihren Signaturen.
    /// </summary>
    /// <remarks>
    /// Eine Signatur gehört zu der Datei, deren Namen sie trägt (<c>&lt;datei&gt;.sig</c>). Seit auch
    /// das Setup signiert ist, liegen zwei <c>.sig</c> im Release; „die erste“ zu nehmen, könnte die
    /// Signatur des Setups gegen das ZIP prüfen. Berücksichtigt werden nur HTTPS-Adressen auf
    /// GitHub-Hosts, damit nichts von einem fremden Server geladen wird.
    /// </remarks>
    private static ReleaseAssets ReadAssets(JsonElement root)
    {
        List<(string Name, string Url)> assets = TrustedAssets(root);
        string? package = PickPackage(assets);
        string? setup = assets.Select(asset => asset.Name).FirstOrDefault(IsSetup);

        return new ReleaseAssets(UrlOf(package), UrlOf(SignatureNameOf(package)), UrlOf(setup), UrlOf(SignatureNameOf(setup)));

        string? UrlOf(string? name)
            => name is null ? null : assets.FirstOrDefault(asset => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase)).Url;
    }

    // Name und Adresse jedes Assets, das über HTTPS von GitHub kommt, in der Reihenfolge der Antwort.
    private static List<(string Name, string Url)> TrustedAssets(JsonElement root)
    {
        List<(string Name, string Url)> trusted = [];
        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return trusted;
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = GetString(asset, "name");
            string? url = GetString(asset, "browser_download_url");
            if (name is not null && url is not null && IsTrustedGitHubUrl(url))
            {
                trusted.Add((name, url));
            }
        }

        return trusted;
    }

    // Bevorzugt ein ZIP mit "portable" im Namen, sonst das erste ZIP.
    private static string? PickPackage(List<(string Name, string Url)> assets)
    {
        List<string> zips = assets
            .Select(asset => asset.Name)
            .Where(name => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return zips.FirstOrDefault(name => name.Contains("portable", StringComparison.OrdinalIgnoreCase)) ?? zips.FirstOrDefault();
    }

    private static bool IsSetup(string name)
        => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && name.Contains("setup", StringComparison.OrdinalIgnoreCase);

    private static string? SignatureNameOf(string? name) => name is null ? null : name + ".sig";

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
