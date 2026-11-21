namespace LookAway.Data.Tests;

/// <summary>
/// Der gemeinsame Dateizugriff der Ablagen: Fehlt Datei oder Ordner, gibt es keinen Inhalt statt
/// einer Ausnahme; die Sicherung beschädigter Inhalte gibt bei Dateifehlern nach.
/// </summary>
public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "lookaway-file-store", TestIds.Next().ToString("N"));

    public JsonFileStoreTests() => _ = Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task ReadAllTextOrNull_WithoutTheFolder_ReturnsNull()
    {
        using JsonFileStore store = new(Path.Combine(_directory, "fehlt", "daten.json"));

        Assert.Null(await store.ReadAllTextOrNullAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadAllTextOrNull_WhenCancelled_ReleasesTheFileAndThrows()
    {
        string path = Path.Combine(_directory, "daten.json");
        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);
        using JsonFileStore store = new(path);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadAllTextOrNullAsync(cancelled.Token));
        // Wieder frei: Nach dem Abbruch lässt sich die Datei exklusiv öffnen.
        using FileStream reopened = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task TryWriteCorruptBackup_WhenTheBackupIsLocked_ReturnsFalse()
    {
        using JsonFileStore store = new(Path.Combine(_directory, "daten.json"));
        using LockedFile locked = new(store.CorruptBackupPath, "alt");

        Assert.False(await store.TryWriteCorruptBackupAsync("kaputt", CancellationToken.None));
    }

    [Fact]
    public async Task TryWriteCorruptBackup_WhenAFolderTakesTheName_ReturnsFalse()
    {
        using JsonFileStore store = new(Path.Combine(_directory, "daten.json"));
        _ = Directory.CreateDirectory(store.CorruptBackupPath);

        Assert.False(await store.TryWriteCorruptBackupAsync("kaputt", CancellationToken.None));
    }

    [Fact]
    public void Dispose_Twice_IsHarmless()
    {
        JsonFileStore store = new(Path.Combine(_directory, "daten.json"));
        store.Dispose();

        Exception? thrown = Record.Exception(store.Dispose);

        Assert.Null(thrown);
    }
}
