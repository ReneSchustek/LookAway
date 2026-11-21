using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Data.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Das Historien-Repository gegen das echte Dateisystem: Anhängen, Aufräumen und eine
/// beschädigte Datei, deren Inhalt gesichert wird, bevor leer weitergearbeitet wird. Ohne die
/// Sicherung überschriebe das nächste Anhängen die alte Historie endgültig.
/// </summary>
public sealed class JsonBreakHistoryRepositoryTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public JsonBreakHistoryRepositoryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "LookAwayHistoryTests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "history.json");
    }

    private JsonBreakHistoryRepository CreateRepository()
        => new(_filePath, NullLogger<JsonBreakHistoryRepository>.Instance);

    private static BreakSession Session(DateTimeOffset start, BreakOutcome outcome = BreakOutcome.Taken)
        => new(TestIds.Next(), start, start.AddMinutes(5), BreakModel.ClassicPomodoro, outcome);

    [Fact]
    public async Task AppendAndLoadAll_StoreTheSessions()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();
        DateTimeOffset now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

        await repository.AppendAsync(Session(now), TestContext.Current.CancellationToken);
        await repository.AppendAsync(Session(now.AddMinutes(30), BreakOutcome.Skipped), TestContext.Current.CancellationToken);

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, all.Count);
        Assert.Equal(BreakOutcome.Skipped, all[1].Outcome);
    }

    [Fact]
    public async Task LoadAll_WithoutFile_ReturnsAnEmptyList()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(all);
    }

    [Fact]
    public async Task PurgeOlderThan_RemovesOldEntries()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();
        DateTimeOffset now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        await repository.AppendAsync(Session(now.AddDays(-400)), TestContext.Current.CancellationToken);
        await repository.AppendAsync(Session(now), TestContext.Current.CancellationToken);

        int removed = await repository.PurgeOlderThanAsync(now.AddDays(-365), TestContext.Current.CancellationToken);

        Assert.Equal(1, removed);
        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync(TestContext.Current.CancellationToken);
        _ = Assert.Single(all);
    }

    [Fact]
    public async Task LoadAll_WithACorruptFile_BacksUpTheContentAndStartsEmpty()
    {
        const string corrupt = "[ this is : not json ::: }";
        await File.WriteAllTextAsync(_filePath, corrupt, TestContext.Current.CancellationToken);
        using JsonBreakHistoryRepository repository = CreateRepository();

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(all);
        string backupPath = _filePath + ".corrupt";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(corrupt, await File.ReadAllTextAsync(backupPath, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Bereits entfernt — nichts zu tun.
        }
    }

    /// <remarks>Die Historie ist nicht startkritisch: Eine gesperrte Datei liest sich als leere Historie.</remarks>
    [Fact]
    public async Task LoadAll_WhenTheFileIsLocked_ReturnsAnEmptyList()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();
        using LockedFile locked = new(_filePath, "[]");

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(all);
    }

    [Fact]
    public async Task LoadAll_WithASessionTheConstructorRejects_BacksUpAndStartsEmpty()
    {
        // Lesbares JSON, aber ohne Kennung: Der Konstruktor der Sitzung lehnt die leere Id ab.
        const string rejected = """[{"id":"00000000-0000-0000-0000-000000000000","startedAt":"2026-06-28T12:00:00+00:00","endedAt":"2026-06-28T12:05:00+00:00","model":"ClassicPomodoro","outcome":"Taken"}]""";
        await File.WriteAllTextAsync(_filePath, rejected, TestContext.Current.CancellationToken);
        using JsonBreakHistoryRepository repository = CreateRepository();

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(all);
        Assert.Equal(rejected, await File.ReadAllTextAsync(_filePath + ".corrupt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PurgeOlderThan_WithNothingToRemove_LeavesTheFileUntouched()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();
        DateTimeOffset now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        await repository.AppendAsync(Session(now), TestContext.Current.CancellationToken);
        DateTime writtenAt = File.GetLastWriteTimeUtc(_filePath);

        int removed = await repository.PurgeOlderThanAsync(now.AddDays(-365), TestContext.Current.CancellationToken);

        Assert.Equal(0, removed);
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(_filePath));
    }

    [Fact]
    public void Dispose_Twice_IsHarmless()
    {
        JsonBreakHistoryRepository repository = CreateRepository();
        repository.Dispose();

        Exception? thrown = Record.Exception(repository.Dispose);

        Assert.Null(thrown);
    }
}
