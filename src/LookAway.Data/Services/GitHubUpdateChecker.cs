using System.Text.Json;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Services;

/// <summary>
/// Prueft die neueste Release ueber die GitHub-Releases-API (BRIEF020). Der
/// eigentliche Netzwerkzugriff ist ueber <see cref="IHttpGetClient"/> gekapselt
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

            UpdateInfo info = UpdateInfo.Create(_currentVersion, tag, htmlUrl, body);
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
}

/// <summary>
/// Source-generierte Logging-Methoden des Update-Checkers.
/// </summary>
internal static partial class GitHubUpdateCheckerLog
{
    [LoggerMessage(EventId = 1600, Level = LogLevel.Information, Message = "Update verfuegbar: Version {Version}.")]
    public static partial void UpdateAvailable(ILogger logger, string version);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Warning, Message = "Antwort der Update-Pruefung konnte nicht ausgewertet werden.")]
    public static partial void ResponseUnparsable(ILogger logger, Exception exception);
}
