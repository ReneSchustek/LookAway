using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
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
}
