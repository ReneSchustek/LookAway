using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Data.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Die Ablagen ohne ausdrücklichen Pfad: Sie legen ihre Dateien im Datenordner ab, den
/// <see cref="AppDataLocation"/> bestimmt. Die Tests stellen dafür einen portablen Basisordner
/// ein, damit nichts im Profil des angemeldeten Benutzers landet.
/// </summary>
[Collection(DataDirectoryTestGroup.Name)]
public sealed class DefaultLocationTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly string _baseDirectory =
        Path.Combine(Path.GetTempPath(), "lookaway-default-location", TestIds.Next().ToString("N"));

    public DefaultLocationTests()
    {
        _ = Directory.CreateDirectory(_baseDirectory);
        File.WriteAllText(Path.Combine(_baseDirectory, AppPaths.PortableFlagFileName), string.Empty);
        AppDataLocation.UseBaseDirectory(_baseDirectory);
    }

    public void Dispose()
    {
        // Zurück auf den Ordner, den die Anwendung ohne Umstellung verwendet.
        AppDataLocation.UseBaseDirectory(AppContext.BaseDirectory);
        Directory.Delete(_baseDirectory, recursive: true);
    }

    [Fact]
    public void PortableMarker_KeepsTheDataNextToTheProgram()
    {
        Assert.True(AppDataLocation.IsPortable());
        Assert.Equal(_baseDirectory, AppDataLocation.GetDataDirectory());
    }

    [Fact]
    public void UseBaseDirectory_RejectsAnEmptyPath()
        => Assert.Throws<ArgumentException>(() => AppDataLocation.UseBaseDirectory(" "));

    [Fact]
    public async Task SettingsRepository_WithoutPath_WritesIntoTheDataDirectory()
    {
        using (JsonSettingsRepository repository = new(NullLogger<JsonSettingsRepository>.Instance))
        {
            await repository.SaveAsync(new Settings(), TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(Path.Combine(_baseDirectory, "settings.json")));
    }

    [Fact]
    public async Task HistoryRepository_WithoutPath_WritesIntoTheDataDirectory()
    {
        using (JsonBreakHistoryRepository repository = new(NullLogger<JsonBreakHistoryRepository>.Instance))
        {
            await repository.AppendAsync(new BreakSession(TestIds.Next(), Now, Now.AddMinutes(5), BreakModel.ClassicPomodoro, BreakOutcome.Taken), TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(Path.Combine(_baseDirectory, "history.json")));
    }

    [Fact]
    public async Task TaskRepository_WithoutPath_WritesIntoTheDataDirectory()
    {
        using (JsonWorkTaskRepository repository = new(NullLogger<JsonWorkTaskRepository>.Instance))
        {
            await repository.SaveAsync(WorkTask.Create("Angebot schreiben", Now), TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(Path.Combine(_baseDirectory, "tasks.json")));
    }

    [Fact]
    public void TimerStateStore_WithoutPath_WritesIntoTheDataDirectory()
    {
        JsonTimerStateStore store = new();

        store.Save(new TimerSnapshot(BreakModel.ClassicPomodoro, TimeSpan.FromMinutes(10), Now));

        Assert.True(File.Exists(Path.Combine(_baseDirectory, "timer-state.json")));
    }
}
