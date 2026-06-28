using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Integration.Data;

/// <summary>
/// Tests fuer den <see cref="GitHubUpdateChecker"/> mit gefaketem HTTP-Zugriff (BRIEF020).
/// </summary>
public sealed class GitHubUpdateCheckerTests
{
    private sealed class FakeHttpGetClient : IHttpGetClient
    {
        private readonly string? _response;

        public FakeHttpGetClient(string? response) => _response = response;

        public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);
    }

    private static GitHubUpdateChecker CreateChecker(string? response, Version current)
        => new(new FakeHttpGetClient(response), current, NullLogger<GitHubUpdateChecker>.Instance);

    [Fact]
    public async Task CheckForUpdate_meldet_neuere_Version()
    {
        const string json = """{ "tag_name": "v2.0.0", "html_url": "https://github.com/x/releases/2.0.0", "body": "Neu" }""";
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync();

        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("2.0.0", info.LatestVersion);
        Assert.NotNull(info.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdate_meldet_kein_Update_bei_gleicher_Version()
    {
        const string json = """{ "tag_name": "v1.0.0", "html_url": "https://github.com/x", "body": "" }""";
        GitHubUpdateChecker checker = CreateChecker(json, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync();

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_behandelt_Netzwerkfehler_als_kein_Update()
    {
        GitHubUpdateChecker checker = CreateChecker(response: null, new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync();

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_behandelt_ungueltiges_JSON_als_kein_Update()
    {
        GitHubUpdateChecker checker = CreateChecker("nicht json", new Version(1, 0, 0));

        UpdateInfo info = await checker.CheckForUpdateAsync();

        Assert.False(info.IsUpdateAvailable);
    }
}
