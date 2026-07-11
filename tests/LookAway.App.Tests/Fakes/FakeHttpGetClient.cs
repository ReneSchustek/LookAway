using LookAway.Core.Interfaces;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// HTTP-Ersatz für Tests, die den Update-Installer nur als Abhängigkeit brauchen und
/// nie etwas herunterladen: liefert nichts und meldet jeden Download als gescheitert.
/// </summary>
internal sealed class FakeHttpGetClient : IHttpGetClient
{
    public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
