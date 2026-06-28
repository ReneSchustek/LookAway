namespace LookAway.Core.Interfaces;

/// <summary>
/// Minimale Abstraktion ueber einen lesenden HTTP-GET-Zugriff (BRIEF020). Erlaubt
/// es, Netzwerkzugriffe in Tests durch einen Fake zu ersetzen.
/// </summary>
public interface IHttpGetClient
{
    /// <summary>
    /// Laedt den Inhalt der URL als Zeichenkette.
    /// </summary>
    /// <param name="requestUri">Abzurufende URL.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Der Antworttext, oder <c>null</c> bei einem Fehler.</returns>
    Task<string?> GetStringAsync(Uri requestUri, CancellationToken cancellationToken = default);
}
