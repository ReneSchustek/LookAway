namespace LookAway.Data;

/// <summary>
/// Hilfen für atomares Ersetzen von Dateien (Temp-Datei + Rename). Geteilt von
/// den JSON-Repositories, damit das Verhalten (Retry, Aufräumen) einheitlich ist.
/// </summary>
internal static class AtomicFile
{
    private const int MaxMoveAttempts = 5;
    private const int RetryBackoffMs = 20;

    /// <summary>
    /// Ersetzt <paramref name="destinationPath"/> durch <paramref name="sourcePath"/>
    /// und wiederholt den Rename bei transienten Sharing-Fehlern (paralleler Reader,
    /// Virenscanner). Schlägt der Rename endgültig fehl, wird die Temp-Datei nach
    /// bestem Bemühen entfernt und die Ausnahme weitergereicht.
    /// </summary>
    /// <param name="sourcePath">Pfad der Temp-Datei.</param>
    /// <param name="destinationPath">Zielpfad.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public static async Task ReplaceWithRetryAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MaxMoveAttempts)
            {
                await Task.Delay(RetryBackoffMs * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxMoveAttempts)
            {
                await Task.Delay(RetryBackoffMs * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Endgültiger Fehlschlag: verwaiste Temp-Datei aufräumen, dann weiterreichen.
                TryDelete(sourcePath);
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                TryDelete(sourcePath);
                throw;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Aufräumen ist best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Aufräumen ist best-effort.
        }
    }
}
