using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// Test-Fake fuer <see cref="IHttpGetClient"/>. <see cref="GetStringAsync"/>
/// liefert eine feste Antwort; <see cref="DownloadFileAsync"/> schreibt einen
/// vorgegebenen Byte-Inhalt in die Zieldatei (oder meldet Misserfolg).
/// </summary>
internal sealed class FakeHttpGetClient : IHttpGetClient
{
    private readonly string? _stringResponse;
    private readonly byte[]? _fileContent;

    public FakeHttpGetClient(string? stringResponse = null, byte[]? fileContent = null)
    {
        _stringResponse = stringResponse;
        _fileContent = fileContent;
    }

    /// <summary>Zahl der Download-Aufrufe.</summary>
    public int DownloadCount { get; private set; }

    public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
        => Task.FromResult(_stringResponse);

    public async Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
    {
        DownloadCount++;
        if (_fileContent is null)
        {
            return false;
        }

        await File.WriteAllBytesAsync(destinationPath, _fileContent, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
