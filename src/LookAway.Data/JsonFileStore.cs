namespace LookAway.Data;

/// <summary>
/// Gemeinsame Datei-Primitive der JSON-Repositories: atomares Schreiben
/// (Temp-Datei + Rename), gegen parallele Renames tolerantes Lesen, ein
/// serialisierender Schreib-Semaphor sowie das Sichern beschädigter Inhalte.
/// </summary>
/// <remarks>
/// Lesezugriffe sind sperrenfrei und öffnen die Datei mit
/// <see cref="FileShare.Read"/> | <see cref="FileShare.Delete"/>, damit ein
/// paralleler atomarer Rename nicht durch ein Reader-Handle blockiert wird.
/// Schreibvorgänge werden über <see cref="RunExclusiveAsync"/> serialisiert.
/// </remarks>
internal sealed class JsonFileStore : IDisposable
{
    private const string TempFileSuffix = ".tmp";
    private const string CorruptFileSuffix = ".corrupt";
    private const int FileBufferSize = 4096;

    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    /// <summary>Erzeugt den Store für einen festen Dateipfad.</summary>
    /// <param name="filePath">Absoluter Pfad der verwalteten Datei.</param>
    public JsonFileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>Pfad der verwalteten Datei.</summary>
    public string FilePath => _filePath;

    /// <summary>Pfad, unter dem beschädigte Inhalte gesichert werden.</summary>
    public string CorruptBackupPath => _filePath + CorruptFileSuffix;

    /// <summary>
    /// Liest den gesamten Dateiinhalt. Liefert <see langword="null"/>, wenn die
    /// Datei oder ihr Verzeichnis nicht existiert; I/O- und Zugriffsfehler werden
    /// weitergereicht, damit der Aufrufer sie kontextbezogen behandeln kann.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Der Inhalt oder <see langword="null"/>, falls nicht vorhanden.</returns>
    public async Task<string?> ReadAllTextOrNullAsync(CancellationToken cancellationToken)
    {
        try
        {
            FileStream stream = new(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using (stream.ConfigureAwait(false))
            {
                using StreamReader reader = new(stream);
                return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Schreibt <paramref name="payload"/> atomar (Temp-Datei + Rename) und legt
    /// das Zielverzeichnis bei Bedarf an. Aufrufer serialisieren über
    /// <see cref="RunExclusiveAsync"/>.
    /// </summary>
    /// <param name="payload">Zu schreibende Bytes.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task WriteBytesAtomicAsync(byte[] payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        string tempPath = _filePath + TempFileSuffix;

        FileStream tempStream = new(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            FileBufferSize,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        await using (tempStream.ConfigureAwait(false))
        {
            await tempStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await tempStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await AtomicFile.ReplaceWithRetryAsync(tempPath, _filePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sichert beschädigten Inhalt unter <see cref="CorruptBackupPath"/>, bevor die
    /// Datei mit Defaults überschrieben wird, damit Daten nicht stillschweigend
    /// verloren gehen. Schreibt den bereits gelesenen Inhalt (statt die Live-Datei
    /// zu verschieben) und ist damit frei von Wettläufen mit parallelen Schreibern.
    /// Best-effort: Fehler werden geschluckt und als <see langword="false"/> gemeldet.
    /// </summary>
    /// <param name="corruptContent">Der nicht interpretierbare Dateiinhalt.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns><see langword="true"/>, wenn die Sicherung geschrieben wurde.</returns>
    public async Task<bool> TryWriteCorruptBackupAsync(string corruptContent, CancellationToken cancellationToken)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(CorruptBackupPath, corruptContent, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Führt <paramref name="action"/> unter dem Schreib-Semaphor aus.</summary>
    /// <param name="action">Die serialisiert auszuführende Aktion.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task RunExclusiveAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }

    /// <summary>Führt <paramref name="action"/> unter dem Schreib-Semaphor aus und liefert ihr Ergebnis.</summary>
    /// <typeparam name="T">Ergebnistyp.</typeparam>
    /// <param name="action">Die serialisiert auszuführende Aktion.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Das Ergebnis von <paramref name="action"/>.</returns>
    public async Task<T> RunExclusiveAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _writeLock.Release();
        }
    }

    /// <summary>Gibt den Schreib-Semaphor frei.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeLock.Dispose();
        _disposed = true;
    }
}
