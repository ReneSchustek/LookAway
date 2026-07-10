using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading;
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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpGetClient> _logger;
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Client mit User-Agent und Timeout.
    /// </summary>
    /// <param name="logger">Logger für Netzwerkfehler.</param>
    public HttpGetClient(ILogger<HttpGetClient> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _httpClient = new HttpClient { Timeout = Timeout };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

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

    // Obergrenze für Downloads (Schutz vor riesigen/bösartigen Dateien).
    private const long MaxDownloadBytes = 512L * 1024 * 1024;

    // 80-KB-Lesepuffer beim Streamen in die Datei — entspricht der internen
    // Standard-Puffergröße von Stream.CopyTo und ist ein bewährter Kompromiss.
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
            // Asset-Downloads (ggf. mehrere hundert MB) dürfen nicht am 10-s-Timeout
            // des Clients scheitern; ein eigener, großzügiger Token wird verkettet.
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(10));
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using HttpResponseMessage response = await _httpClient
                .GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            // Eine etwaige Weiterleitung darf nicht auf Klartext (HTTP) herabstufen.
            Uri? finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is not null
                && !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                HttpGetClientLog.DownloadRejected(_logger, finalUri.ToString());
                return false;
            }

            // Früher Abbruch, wenn die angekündigte Größe das Limit sprengt.
            if (response.Content.Headers.ContentLength is long announced && announced > MaxDownloadBytes)
            {
                HttpGetClientLog.DownloadTooLarge(_logger, requestUri.ToString(), announced);
                return false;
            }

            using FileStream file = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using Stream source = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);

            byte[] buffer = new byte[DownloadBufferSize];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, linked.Token).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxDownloadBytes)
                {
                    HttpGetClientLog.DownloadTooLarge(_logger, requestUri.ToString(), total);
                    return false;
                }

                await file.WriteAsync(buffer.AsMemory(0, read), linked.Token).ConfigureAwait(false);
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            HttpGetClientLog.DownloadFailed(_logger, ex, requestUri.ToString());
            return false;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Der großzügige Download-Timeout ist abgelaufen, nicht der Aufrufer-Token.
            HttpGetClientLog.DownloadFailed(_logger, ex, requestUri.ToString());
            return false;
        }
        catch (IOException ex)
        {
            HttpGetClientLog.DownloadFailed(_logger, ex, requestUri.ToString());
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            HttpGetClientLog.DownloadFailed(_logger, ex, requestUri.ToString());
            return false;
        }
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
