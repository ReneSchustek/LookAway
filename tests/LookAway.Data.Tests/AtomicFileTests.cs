namespace LookAway.Data.Tests;

/// <summary>
/// Tests für <see cref="AtomicFile"/>: das Ersetzen einer Datei über eine Zwischendatei
/// samt Wiederholung, wenn das Ziel gerade belegt ist.
/// </summary>
public sealed class AtomicFileTests : IDisposable
{
    private readonly string _directory;
    private readonly string _sourcePath;
    private readonly string _destinationPath;

    public AtomicFileTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "LookAwayAtomic", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_directory);
        _sourcePath = Path.Combine(_directory, "neu.tmp");
        _destinationPath = Path.Combine(_directory, "ziel.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
        catch (UnauthorizedAccessException)
        {
            // best effort
        }
    }

    [Fact]
    public async Task ReplacesTheDestinationAndRemovesTheSource()
    {
        await File.WriteAllTextAsync(_destinationPath, "alt");
        await File.WriteAllTextAsync(_sourcePath, "neu");

        await AtomicFile.ReplaceWithRetryAsync(_sourcePath, _destinationPath, CancellationToken.None);

        Assert.Equal("neu", await File.ReadAllTextAsync(_destinationPath));
        Assert.False(File.Exists(_sourcePath));
    }

    [Fact]
    public async Task WritesTheDestinationWhenItDoesNotExistYet()
    {
        await File.WriteAllTextAsync(_sourcePath, "neu");

        await AtomicFile.ReplaceWithRetryAsync(_sourcePath, _destinationPath, CancellationToken.None);

        Assert.Equal("neu", await File.ReadAllTextAsync(_destinationPath));
    }

    /// <remarks>
    /// Der Fall, für den die Wiederholung überhaupt da ist: Ein Virenscanner oder ein
    /// zweiter Leser hält die Zieldatei für einen Moment. Der Test gibt sie nach kurzer
    /// Zeit wieder frei; die Wartezeiten zwischen den Versuchen summieren sich auf ein
    /// Vielfaches davon, sodass der Ersatz gelingen muss.
    /// </remarks>
    [Fact]
    public async Task RetriesWhileTheDestinationIsBusy()
    {
        await File.WriteAllTextAsync(_destinationPath, "alt");
        await File.WriteAllTextAsync(_sourcePath, "neu");

        FileStream blocker = new(_destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Task replacing = AtomicFile.ReplaceWithRetryAsync(_sourcePath, _destinationPath, CancellationToken.None);

        await Task.Delay(30);
        await blocker.DisposeAsync();

        await replacing.ConfigureAwait(true);

        Assert.Equal("neu", await File.ReadAllTextAsync(_destinationPath));
    }

    /// <remarks>
    /// Bleibt die Datei belegt, gibt die Wiederholung auf. Der Fehler wird
    /// weitergereicht — der Aufrufer muss erfahren, dass nichts gesichert wurde —,
    /// aber die Zwischendatei bleibt nicht liegen. Sonst sammelten sich bei einer
    /// dauerhaft belegten Datei mit jedem Versuch neue Reste an.
    ///
    /// Welche der beiden Ausnahmen Windows für eine belegte Datei meldet, hängt davon
    /// ab, woran das Verschieben scheitert; beide bedeuten dasselbe und werden im Code
    /// gleich behandelt. Der Test legt sich deshalb nicht auf eine fest.
    /// </remarks>
    [Fact]
    public async Task GivesUpOnAPermanentlyBusyDestinationAndCleansUp()
    {
        await File.WriteAllTextAsync(_destinationPath, "alt");
        await File.WriteAllTextAsync(_sourcePath, "neu");

        using LockedFile locked = new(_destinationPath, "alt");

        Exception thrown = await Assert.ThrowsAnyAsync<Exception>(
            () => AtomicFile.ReplaceWithRetryAsync(_sourcePath, _destinationPath, CancellationToken.None));

        Assert.True(thrown is IOException or UnauthorizedAccessException, thrown.GetType().Name);
        Assert.False(File.Exists(_sourcePath));
    }

    /// <remarks>
    /// Der umgekehrte Fall: Nicht das Ziel, sondern die Zwischendatei ist belegt. Dann
    /// scheitert auch das Aufräumen — und muss ebenfalls schweigen, sonst verdeckte
    /// eine Ausnahme aus dem Aufräumen den eigentlichen Fehler.
    /// </remarks>
    [Fact]
    public async Task KeepsTheOriginalErrorWhenCleanupFailsToo()
    {
        await File.WriteAllTextAsync(_destinationPath, "alt");
        using LockedFile locked = new(_sourcePath, "neu");

        Exception thrown = await Assert.ThrowsAnyAsync<Exception>(
            () => AtomicFile.ReplaceWithRetryAsync(_sourcePath, _destinationPath, CancellationToken.None));

        Assert.True(thrown is IOException or UnauthorizedAccessException, thrown.GetType().Name);
        Assert.Equal("alt", await File.ReadAllTextAsync(_destinationPath));
    }
}
