namespace LookAway.Data;

/// <summary>
/// Hilfen fuer atomares Ersetzen von Dateien (Temp-Datei + Rename). Geteilt von
/// den JSON-Repositories, damit das Verhalten (Retry, Aufraeumen) einheitlich ist.
/// </summary>
internal static class AtomicFile
{
    private const int MaxMoveAttempts = 5;
    private const int RetryBackoffMs = 20;

    /// <summary>
    /// Ersetzt <paramref name="destinationPath"/> durch <paramref name="sourcePath"/>
    /// und wiederholt den Rename bei transienten Sharing-Fehlern (paralleler Reader,
    /// Virenscanner). Schlaegt der Rename endgueltig fehl, wird die Temp-Datei nach
    /// bestem Bemuehen entfernt und die Ausnahme weitergereicht.
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
                // Endgueltiger Fehlschlag: verwaiste Temp-Datei aufraeumen, dann weiterreichen.
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
            // Aufraeumen ist best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Aufraeumen ist best-effort.
        }
    }
}
