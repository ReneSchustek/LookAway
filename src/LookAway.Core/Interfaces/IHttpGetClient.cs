namespace LookAway.Core.Interfaces;

/// <summary>
/// Minimale Abstraktion ueber einen lesenden HTTP-GET-Zugriff. Erlaubt
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

    /// <summary>
    /// Laedt den Inhalt der URL in eine Datei. Folgt Weiterleitungen (z. B. von
    /// GitHub-Release-Assets) und ueberschreibt eine bestehende Zieldatei.
    /// </summary>
    /// <param name="requestUri">Abzurufende URL.</param>
    /// <param name="destinationPath">Zielpfad der Datei.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns><c>true</c> bei Erfolg, sonst <c>false</c>.</returns>
    Task<bool> DownloadFileAsync(Uri requestUri, string destinationPath, CancellationToken cancellationToken = default);
}
