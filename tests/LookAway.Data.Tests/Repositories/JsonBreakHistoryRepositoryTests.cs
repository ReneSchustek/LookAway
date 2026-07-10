using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Data.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Tests für das <see cref="JsonBreakHistoryRepository"/> gegen das echte
/// Dateisystem.
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
        => new(Guid.NewGuid(), start, start.AddMinutes(5), BreakModel.ClassicPomodoro, outcome);

    [Fact]
    public async Task Append_und_LoadAll_speichern_die_Sitzungen()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await repository.AppendAsync(Session(now));
        await repository.AppendAsync(Session(now.AddMinutes(30), BreakOutcome.Skipped));

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(BreakOutcome.Skipped, all[1].Outcome);
    }

    [Fact]
    public async Task LoadAll_liefert_leere_Liste_ohne_Datei()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task PurgeOlderThan_entfernt_alte_Einträge()
    {
        using JsonBreakHistoryRepository repository = CreateRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await repository.AppendAsync(Session(now.AddDays(-400)));
        await repository.AppendAsync(Session(now));

        int removed = await repository.PurgeOlderThanAsync(now.AddDays(-365));

        Assert.Equal(1, removed);
        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync();
        _ = Assert.Single(all);
    }

    [Fact]
    public async Task LoadAll_mit_beschädigter_Datei_sichert_Inhalt_und_startet_leer()
    {
        const string corrupt = "[ this is : not json ::: }";
        await File.WriteAllTextAsync(_filePath, corrupt);
        using JsonBreakHistoryRepository repository = CreateRepository();

        IReadOnlyList<BreakSession> all = await repository.LoadAllAsync();

        Assert.Empty(all);
        string backupPath = _filePath + ".corrupt";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(corrupt, await File.ReadAllTextAsync(backupPath));
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
}
