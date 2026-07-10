using LookAway.Core.Interfaces;

namespace LookAway.Data.Tests.Fakes;

/// <summary>
/// Test-Fake für <see cref="IHttpGetClient"/>. <see cref="GetStringAsync"/> liefert
/// eine feste Antwort. <see cref="DownloadFileAsync"/> schreibt entweder einen pro
/// URL hinterlegten Inhalt (für Szenarien mit mehreren Assets wie Paket + Signatur)
/// oder — wenn keine URL-Tabelle angegeben ist — einen einzelnen festen Inhalt;
/// fehlt der passende Inhalt, wird Misserfolg gemeldet.
/// </summary>
internal sealed class FakeHttpGetClient : IHttpGetClient
{
    private readonly string? _stringResponse;
    private readonly byte[]? _fileContent;
    private readonly IReadOnlyDictionary<string, byte[]>? _filesByUrl;

    /// <summary>Fake mit höchstens einem festen Download-Inhalt für jede URL.</summary>
    /// <param name="stringResponse">Antwort von <see cref="GetStringAsync"/>.</param>
    /// <param name="fileContent">Inhalt jedes Datei-Downloads, oder <c>null</c> für Misserfolg.</param>
    public FakeHttpGetClient(string? stringResponse = null, byte[]? fileContent = null)
    {
        _stringResponse = stringResponse;
        _fileContent = fileContent;
    }

    /// <summary>Fake, der den Download-Inhalt anhand der absoluten URL nachschlägt.</summary>
    /// <param name="filesByUrl">Zuordnung absolute URL → Dateiinhalt.</param>
    /// <param name="stringResponse">Antwort von <see cref="GetStringAsync"/>.</param>
    public FakeHttpGetClient(IReadOnlyDictionary<string, byte[]> filesByUrl, string? stringResponse = null)
    {
        ArgumentNullException.ThrowIfNull(filesByUrl);
        _filesByUrl = filesByUrl;
        _stringResponse = stringResponse;
    }

    /// <summary>Zahl der Download-Aufrufe.</summary>
    public int DownloadCount { get; private set; }

    public Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
        => Task.FromResult(_stringResponse);

    public async Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        DownloadCount++;

        // URL-Tabelle hat Vorrang: nur bekannte URLs liefern Inhalt, alles andere
        // meldet Misserfolg (so lassen sich fehlende Assets gezielt testen).
        byte[]? content = _filesByUrl is not null
            ? (_filesByUrl.TryGetValue(requestUri.AbsoluteUri, out byte[]? mapped) ? mapped : null)
            : _fileContent;

        if (content is null)
        {
            return false;
        }

        await File.WriteAllBytesAsync(destinationPath, content, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
