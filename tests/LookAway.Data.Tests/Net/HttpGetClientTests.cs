using System.Net;
using System.Net.Http;
using LookAway.Data.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Tests für <see cref="HttpGetClient"/>: Fehler werden zu <c>null</c> oder <c>false</c> statt zu
/// Ausnahmen, und ein Download gelangt nur über HTTPS und unter der Größengrenze auf die Platte.
/// Ein eigener Handler liefert die Antworten; kein Test geht ins Netz.
/// </summary>
public sealed class HttpGetClientTests : IDisposable
{
    private static readonly Uri ApiUri = new("https://api.github.com/repos/x/y/releases/latest");
    private static readonly Uri PackageUri = new("https://github.com/x/y/releases/download/v1/LookAway.zip");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lookaway-http-tests", TestIds.Next().ToString("N"));

    public HttpGetClientTests() => _ = Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // Antwortet auf jede Anfrage mit dem, was der Test vorgibt, und merkt sich die Anfrage.
    private sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return respond(request, cancellationToken);
        }
    }

    // Der HttpClient übernimmt den Handler; die Tests entsorgen ihn zusätzlich selbst, was ihm nicht schadet.
    // Den HttpClient übernimmt der HttpGetClient; nur wenn dessen Aufbau scheitert, bleibt er hier zu entsorgen.
    private static HttpGetClient Create(ScriptedHandler handler, long maxDownloadBytes = 1024)
    {
        HttpClient? httpClient = new(handler);
        try
        {
            HttpGetClient client = new(httpClient, NullLogger<HttpGetClient>.Instance, maxDownloadBytes);
            httpClient = null;
            return client;
        }
        finally
        {
            httpClient?.Dispose();
        }
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Reply(
        HttpStatusCode status,
        byte[]? body = null,
        Uri? finalUri = null)
        => (request, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(body ?? []),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUri ?? request.RequestUri),
        });

    private string Destination => Path.Combine(_directory, "paket.zip");

    [Fact]
    public async Task GetStringAsync_ReturnsTheBodyAndSendsTheUserAgent()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK, "{\"tag_name\":\"v1.5.0\"}"u8.ToArray()));
        using HttpGetClient client = Create(handler);

        string? body = await client.GetStringAsync(ApiUri, TestContext.Current.CancellationToken);

        Assert.Equal("{\"tag_name\":\"v1.5.0\"}", body);
        Assert.Contains("LookAway-UpdateChecker", handler.LastRequest!.Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStringAsync_OnAServerError_ReturnsNull()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.InternalServerError));
        using HttpGetClient client = Create(handler);

        Assert.Null(await client.GetStringAsync(ApiUri, TestContext.Current.CancellationToken));
    }

    /// <remarks>
    /// Ein Abbruch, den nicht der Aufrufer ausgelöst hat, ist der Timeout des Clients. Er gilt
    /// wie ein Netzfehler: Die Prüfung meldet „kein Ergebnis“, statt die App zu stören.
    /// </remarks>
    [Fact]
    public async Task GetStringAsync_WhenTheRequestTimesOut_ReturnsNull()
    {
        using ScriptedHandler handler = new((_, _) => throw new TaskCanceledException("Timeout"));
        using HttpGetClient client = Create(handler);

        Assert.Null(await client.GetStringAsync(ApiUri, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetStringAsync_WhenTheCallerCancels_Throws()
    {
        using ScriptedHandler handler = new((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        using HttpGetClient client = Create(handler);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetStringAsync(ApiUri, cancelled.Token));
    }

    [Fact]
    public async Task DownloadFileAsync_WritesTheBodyToTheDestination()
    {
        byte[] package = [1, 2, 3, 4, 5];
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK, package));
        using HttpGetClient client = Create(handler);

        bool result = await client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(package, await File.ReadAllBytesAsync(Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadFileAsync_RejectsNonHttpsScheme_ReturnsFalseAndWritesNothing()
    {
        int requests = 0;
        using ScriptedHandler handler = new((request, token) =>
        {
            requests++;
            return Reply(HttpStatusCode.OK)(request, token);
        });
        using HttpGetClient client = Create(handler);

        bool result = await client.DownloadFileAsync(new Uri("http://github.com/x.zip"), Destination, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(File.Exists(Destination));
        Assert.Equal(0, requests);
    }

    /// <remarks>
    /// GitHub leitet Release-Downloads weiter. Endet die Weiterleitung auf Klartext, könnte
    /// jemand dazwischen das Paket tauschen; dann wird nichts geschrieben.
    /// </remarks>
    [Fact]
    public async Task DownloadFileAsync_WhenARedirectEndsOnHttp_WritesNothing()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK, [1, 2, 3], new Uri("http://objects.example.com/paket.zip")));
        using HttpGetClient client = Create(handler);

        Assert.False(await client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Destination));
    }

    [Fact]
    public async Task DownloadFileAsync_WhenTheAnnouncedSizeIsTooLarge_WritesNothing()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK, new byte[2048]));
        using HttpGetClient client = Create(handler, maxDownloadBytes: 1024);

        Assert.False(await client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Destination));
    }

    /// <remarks>
    /// Die angekündigte Größe stammt vom Server und kann fehlen oder lügen. Gezählt wird, was
    /// tatsächlich ankommt; jenseits der Grenze bricht der Download ab.
    /// </remarks>
    [Fact]
    public async Task DownloadFileAsync_WhenTheStreamExceedsTheLimitWithoutAnnouncingIt_ReturnsFalse()
    {
        using ScriptedHandler handler = new((request, _) =>
        {
            StreamContent content = new(new MemoryStream(new byte[4096]));
            content.Headers.ContentLength = null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, request.RequestUri),
            });
        });
        using HttpGetClient client = Create(handler, maxDownloadBytes: 1024);

        Assert.False(await client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadFileAsync_OnANotFound_ReturnsFalse()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.NotFound));
        using HttpGetClient client = Create(handler);

        Assert.False(await client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadFileAsync_WhenTheRequestTimesOut_ReturnsFalse()
    {
        using ScriptedHandler handler = new((_, _) => throw new TaskCanceledException("Timeout"));
        using HttpGetClient client = Create(handler);

        Assert.False(await client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadFileAsync_WhenTheDestinationCannotBeWritten_ReturnsFalse()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK, [1, 2, 3]));
        using HttpGetClient client = Create(handler);
        string missingFolder = Path.Combine(_directory, "fehlt", "paket.zip");

        Assert.False(await client.DownloadFileAsync(PackageUri, missingFolder, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadFileAsync_WhenTheDestinationIsLocked_ReturnsFalse()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK, [1, 2, 3]));
        using HttpGetClient client = Create(handler);
        using LockedFile locked = new(Destination);

        Assert.False(await client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AfterDispose_CallsThrow_AndASecondDisposeIsHarmless()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK));
        HttpGetClient client = Create(handler);
        client.Dispose();
        client.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetStringAsync(ApiUri, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => client.DownloadFileAsync(PackageUri, Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PrimaryHandler_LimitsTheLifetimeOfPooledConnections()
    {
        using SocketsHttpHandler handler = HttpGetClient.CreatePrimaryHandler();

        Assert.Equal(TimeSpan.FromMinutes(15), handler.PooledConnectionLifetime);
    }

    [Fact]
    public void PublicConstructor_SetsUserAgentAndTimeoutOnTheGivenClient()
    {
        using ScriptedHandler handler = new(Reply(HttpStatusCode.OK));
        using HttpClient httpClient = new(handler);
        using HttpGetClient client = new(httpClient, NullLogger<HttpGetClient>.Instance);

        Assert.Equal(TimeSpan.FromSeconds(10), httpClient.Timeout);
        Assert.Contains(httpClient.DefaultRequestHeaders.UserAgent, product => product.Product?.Name == "LookAway-UpdateChecker");
    }

    [Fact]
    public void PublicConstructor_RejectsAMissingClient()
        => Assert.Throws<ArgumentNullException>(() => new HttpGetClient(null!, NullLogger<HttpGetClient>.Instance));

    [Fact]
    public void PublicConstructor_RejectsAMissingLogger()
    {
        using HttpClient httpClient = new();

        _ = Assert.Throws<ArgumentNullException>(() => new HttpGetClient(httpClient, null!));
    }
}
