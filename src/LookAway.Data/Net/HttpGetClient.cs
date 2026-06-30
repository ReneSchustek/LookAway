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
/// Netzwerkfehler fuehrt zu <c>null</c> statt zu einer Exception.
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
    /// <param name="logger">Logger fuer Netzwerkfehler.</param>
    public HttpGetClient(ILogger<HttpGetClient> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _httpClient = new HttpClient { Timeout = Timeout };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Die Update-Pruefung ist unkritisch: jeder Fehler wird geloggt und als 'kein Ergebnis' (null) behandelt, damit Netzwerkprobleme die App nie blockieren.")]
    public async Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _httpClient.GetStringAsync(requestUri, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            HttpGetClientLog.RequestFailed(_logger, ex, requestUri.ToString());
            return null;
        }
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Der Download ist unkritisch: jeder Fehler wird geloggt und als Misserfolg (false) behandelt, damit Netzwerk-/Dateifehler die App nie zum Absturz bringen.")]
    public async Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Asset-Downloads (ggf. mehrere hundert MB) duerfen nicht am 10-s-Timeout
            // des Clients scheitern; ein eigener, grosszuegiger Token wird verkettet.
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(10));
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using HttpResponseMessage response = await _httpClient
                .GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            using FileStream file = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(file, linked.Token).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
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
}
