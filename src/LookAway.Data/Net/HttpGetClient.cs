using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Net;

/// <summary>
/// <see cref="IHttpGetClient"/> auf Basis von <see cref="HttpClient"/>.
/// Setzt den von der GitHub-API geforderten User-Agent und einen Timeout; jeder
/// Netzwerkfehler führt zu <c>null</c> statt zu einer Exception.
/// </summary>
public sealed class HttpGetClient : IHttpGetClient, IDisposable
{
    private const string UserAgent = "LookAway-UpdateChecker/1.0";

    // Die API-Antwort ist wenige Kilobyte groß. Zehn Sekunden reichen dafür auch über eine
    // langsame Leitung; ein hängendes Netz hält die Prüfung nicht länger auf.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpGetClient> _logger;
    private readonly long _maxDownloadBytes;
    private bool _disposed;

    /// <summary>
    /// Name, unter dem die App den zugrunde liegenden <see cref="HttpClient"/> bei der
    /// <c>IHttpClientFactory</c> anmeldet.
    /// </summary>
    public const string ClientName = "LookAway.Update";

    // Ohne begrenzte Verbindungsdauer hielte ein Handler eine Verbindung über DNS-Wechsel hinweg
    // fest; eine Viertelstunde ist die übliche Vorgabe dafür.
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Übernimmt einen <see cref="HttpClient"/> und setzt User-Agent und Timeout.
    /// </summary>
    /// <param name="httpClient">Client aus der Factory; der <see cref="HttpGetClient"/> entsorgt ihn.</param>
    /// <param name="logger">Logger für Netzwerkfehler.</param>
    public HttpGetClient(HttpClient httpClient, ILogger<HttpGetClient> logger)
        : this(httpClient, logger, MaxDownloadBytes)
    {
    }

    /// <summary>
    /// Wie der öffentliche Konstruktor, aber mit eigener Größengrenze (für Tests: Grenzfälle ohne
    /// ein halbes Gigabyte Daten).
    /// </summary>
    /// <param name="httpClient">Client, den der <see cref="HttpGetClient"/> übernimmt und entsorgt.</param>
    /// <param name="logger">Logger für Netzwerkfehler.</param>
    /// <param name="maxDownloadBytes">Obergrenze eines Downloads in Bytes.</param>
    internal HttpGetClient(HttpClient httpClient, ILogger<HttpGetClient> logger, long maxDownloadBytes)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDownloadBytes, 1);
        _httpClient = httpClient;
        _logger = logger;
        _maxDownloadBytes = maxDownloadBytes;
        _httpClient.Timeout = Timeout;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    /// <summary>
    /// Baut den Handler, den die Factory für diesen Client verwenden soll.
    /// </summary>
    /// <remarks>
    /// Der <see cref="HttpGetClient"/> lebt als Singleton so lange wie die App und hält seinen
    /// <see cref="HttpClient"/> fest; die Factory tauscht dessen Handler darum nie aus. Die
    /// begrenzte Verbindungsdauer übernimmt deshalb das Erneuern der Verbindungen.
    /// </remarks>
    /// <returns>Ein neuer Handler mit begrenzter Verbindungsdauer.</returns>
    public static SocketsHttpHandler CreatePrimaryHandler()
        => new() { PooledConnectionLifetime = PooledConnectionLifetime };

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _httpClient.GetStringAsync(requestUri, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            HttpGetClientLog.RequestFailed(_logger, ex, requestUri.ToString());
            return null;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Nicht der Aufrufer hat abgebrochen, sondern der Client-Timeout ist abgelaufen.
            HttpGetClientLog.RequestFailed(_logger, ex, requestUri.ToString());
            return null;
        }
    }

    // Obergrenze für Downloads, mit reichlich Abstand über dem Portable-Paket. Was größer
    // ist, ist kein Update, sondern ein Versuch, die Platte zu füllen.
    private const long MaxDownloadBytes = 512L * 1024 * 1024;

    // Die Puffergröße von Stream.CopyTo; knapp unter der Grenze zum Large Object Heap.
    private const int DownloadBufferSize = 81920;

    /// <inheritdoc />
    public async Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Nur HTTPS — kein Klartext-Download ausführbarer Inhalte.
        if (!string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            HttpGetClientLog.DownloadRejected(_logger, requestUri.ToString());
            return false;
        }

        try
        {
            return await DownloadCheckedAsync(requestUri, destinationPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsDownloadFailure(ex, cancellationToken))
        {
            HttpGetClientLog.DownloadFailed(_logger, ex, requestUri.ToString());
            return false;
        }
    }

    // Netz-, Datei- und Rechtefehler und ein abgelaufener Download-Timeout führen zu false. Bricht
    // dagegen der Aufrufer ab, geht der Abbruch an ihn weiter.
    private static bool IsDownloadFailure(Exception exception, CancellationToken callerToken)
        => exception is HttpRequestException or IOException or UnauthorizedAccessException
            || (exception is OperationCanceledException && !callerToken.IsCancellationRequested);

    private async Task<bool> DownloadCheckedAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken)
    {
        // Mit ResponseHeadersRead gilt der 10-s-Timeout des Clients nur bis zu den
        // Antwortköpfen. Den Datenstrom begrenzt dieser Token auf zehn Minuten; ohne ihn
        // könnte ein Server, der Byte für Byte tröpfelt, die Aktualisierung endlos halten.
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(10));
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using HttpResponseMessage response = await _httpClient
            .GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, linked.Token)
            .ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        if (!IsAcceptableResponse(response, requestUri))
        {
            return false;
        }

        using Stream source = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
        return await StreamToFileAsync(source, destinationPath, requestUri, linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Prüft die Antwort, bevor auch nur ein Byte auf die Platte geht.
    /// </summary>
    /// <param name="response">Die Antwort des Servers.</param>
    /// <param name="requestUri">Die ursprünglich angefragte Adresse (für das Protokoll).</param>
    /// <returns><c>true</c>, wenn heruntergeladen werden darf.</returns>
    private bool IsAcceptableResponse(HttpResponseMessage response, Uri requestUri)
    {
        // Eine etwaige Weiterleitung darf nicht auf Klartext (HTTP) herabstufen.
        Uri? finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null
            && !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            HttpGetClientLog.DownloadRejected(_logger, finalUri.ToString());
            return false;
        }

        // Früher Abbruch, wenn die angekündigte Größe das Limit sprengt.
        if (response.Content.Headers.ContentLength is long announced && announced > _maxDownloadBytes)
        {
            HttpGetClientLog.DownloadTooLarge(_logger, requestUri.ToString(), announced);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Schreibt den Strom in die Zieldatei und bricht ab, sobald die Obergrenze
    /// überschritten wird.
    /// </summary>
    /// <param name="source">Der Strom aus der Antwort.</param>
    /// <param name="destinationPath">Zielpfad.</param>
    /// <param name="requestUri">Angefragte Adresse (für das Protokoll).</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns><c>true</c>, wenn die Datei vollständig geschrieben wurde.</returns>
    /// <remarks>
    /// Gezählt werden die tatsächlich gelesenen Bytes: Die angekündigte Größe hat der
    /// Server geschickt und kann jede Zahl tragen.
    /// </remarks>
    private async Task<bool> StreamToFileAsync(
        Stream source,
        string destinationPath,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using FileStream file = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[DownloadBufferSize];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > _maxDownloadBytes)
            {
                HttpGetClientLog.DownloadTooLarge(_logger, requestUri.ToString(), total);
                return false;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Gibt den HttpClient frei.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des HTTP-Clients.
/// </summary>
internal static partial class HttpGetClientLog
{
    [LoggerMessage(EventId = 1610, Level = LogLevel.Warning, Message = "HTTP-GET auf {Uri} fehlgeschlagen.")]
    public static partial void RequestFailed(ILogger logger, Exception exception, string uri);

    [LoggerMessage(EventId = 1611, Level = LogLevel.Warning, Message = "Datei-Download von {Uri} fehlgeschlagen.")]
    public static partial void DownloadFailed(ILogger logger, Exception exception, string uri);

    [LoggerMessage(EventId = 1612, Level = LogLevel.Warning, Message = "Datei-Download von {Uri} abgelehnt (kein HTTPS).")]
    public static partial void DownloadRejected(ILogger logger, string uri);

    [LoggerMessage(EventId = 1613, Level = LogLevel.Warning, Message = "Datei-Download von {Uri} abgebrochen: zu groß ({Bytes} Bytes).")]
    public static partial void DownloadTooLarge(ILogger logger, string uri, long bytes);
}
