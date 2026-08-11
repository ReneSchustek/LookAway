using LookAway.Core.Entities;
using LookAway.Data.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests.Repositories;

/// <summary>
/// Tests für <see cref="JsonWorkTaskRepository"/>: Anlegen, Ändern, Löschen und das
/// Verhalten bei fehlender oder beschädigter Datei.
/// </summary>
public sealed class JsonWorkTaskRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _directory;
    private readonly string _filePath;

    /// <summary>Legt ein eigenes Verzeichnis für den Testlauf an.</summary>
    public JsonWorkTaskRepositoryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "lookaway-tasks-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "tasks.json");
    }

    [Fact]
    public async Task LoadAllAsync_ReturnsEmptyWhenNothingWasSaved()
    {
        using JsonWorkTaskRepository repository = CreateRepository();

        Assert.Empty(await repository.LoadAllAsync());
    }

    [Fact]
    public async Task SaveAsync_PersistsAcrossInstances()
    {
        WorkTask task = WorkTask.Create("Angebot schreiben", Now);
        using (JsonWorkTaskRepository writer = CreateRepository())
        {
            await writer.SaveAsync(task);
        }

        using JsonWorkTaskRepository reader = CreateRepository();
        WorkTask loaded = Assert.Single(await reader.LoadAllAsync());

        Assert.Equal(task.Id, loaded.Id);
        Assert.Equal("Angebot schreiben", loaded.Text);
        Assert.False(loaded.IsCompleted);
    }

    /// <remarks>
    /// Dieselbe Kennung ersetzt den Eintrag an Ort und Stelle. Würde er angehängt,
    /// stünde die Aufgabe nach jedem Abhaken an anderer Stelle in der Liste.
    /// </remarks>
    [Fact]
    public async Task SaveAsync_ReplacesInPlace()
    {
        using JsonWorkTaskRepository repository = CreateRepository();
        WorkTask first = WorkTask.Create("Erste", Now);
        WorkTask second = WorkTask.Create("Zweite", Now);
        await repository.SaveAsync(first);
        await repository.SaveAsync(second);

        await repository.SaveAsync(first.Complete(Now.AddHours(1)));

        IReadOnlyList<WorkTask> all = await repository.LoadAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(first.Id, all[0].Id);
        Assert.True(all[0].IsCompleted);
        Assert.Equal(second.Id, all[1].Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheTask()
    {
        using JsonWorkTaskRepository repository = CreateRepository();
        WorkTask task = WorkTask.Create("Wieder weg", Now);
        await repository.SaveAsync(task);

        Assert.True(await repository.DeleteAsync(task.Id));
        Assert.Empty(await repository.LoadAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReportsFalseForAnUnknownId()
    {
        using JsonWorkTaskRepository repository = CreateRepository();

        Assert.False(await repository.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task LoadAllAsync_KeepsTheCompletionTime()
    {
        using JsonWorkTaskRepository repository = CreateRepository();
        await repository.SaveAsync(WorkTask.Create("Erledigt", Now).Complete(Now.AddHours(3)));

        WorkTask loaded = Assert.Single(await repository.LoadAllAsync());

        Assert.True(loaded.IsCompleted);
        Assert.Equal(Now.AddHours(3), loaded.CompletedAt);
    }

    /// <remarks>
    /// Eine beschädigte Datei darf die Ansicht nicht blockieren — sie wird gesichert
    /// und leer behandelt. Sonst käme man an die Aufgaben gar nicht mehr heran.
    /// </remarks>
    [Fact]
    public async Task LoadAllAsync_TreatsACorruptFileAsEmpty()
    {
        await File.WriteAllTextAsync(_filePath, "{kein gültiges JSON");
        using JsonWorkTaskRepository repository = CreateRepository();

        Assert.Empty(await repository.LoadAllAsync());
    }

    [Fact]
    public async Task SaveAsync_WorksAfterACorruptFile()
    {
        await File.WriteAllTextAsync(_filePath, "[[[");
        using JsonWorkTaskRepository repository = CreateRepository();

        await repository.SaveAsync(WorkTask.Create("Neu nach Schaden", Now));

        Assert.Equal("Neu nach Schaden", Assert.Single(await repository.LoadAllAsync()).Text);
    }

    [Fact]
    public void Constructor_RejectsAnEmptyPath()
        => Assert.Throws<ArgumentException>(
            () => new JsonWorkTaskRepository("  ", NullLogger<JsonWorkTaskRepository>.Instance));

    [Fact]
    public async Task SaveAsync_RejectsNull()
    {
        using JsonWorkTaskRepository repository = CreateRepository();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SaveAsync(null!));
    }

    private JsonWorkTaskRepository CreateRepository()
        => new(_filePath, NullLogger<JsonWorkTaskRepository>.Instance);

    /// <summary>Räumt das Testverzeichnis wieder ab.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen ist bestes Bemühen.
        }
    }
}
