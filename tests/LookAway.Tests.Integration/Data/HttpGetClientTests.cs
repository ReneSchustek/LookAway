using LookAway.Data.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Integration.Data;

/// <summary>
/// Tests fuer die Sicherheits-Vorbedingungen von <see cref="HttpGetClient"/>.
/// Der HTTPS-Zwang greift vor jedem Netzwerkzugriff und ist daher ohne Server testbar.
/// </summary>
public sealed class HttpGetClientTests
{
    [Fact]
    public async Task DownloadFileAsync_RejectsNonHttpsScheme_ReturnsFalseAndWritesNothing()
    {
        using HttpGetClient client = new(NullLogger<HttpGetClient>.Instance);
        string destination = Path.Combine(Path.GetTempPath(), $"lookaway-http-test-{Guid.NewGuid():N}.zip");

        bool result = await client.DownloadFileAsync(new Uri("http://github.com/x.zip"), destination);

        Assert.False(result);
        Assert.False(File.Exists(destination));
    }
}
