using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Data.Repositories;

namespace LookAway.Data.Tests;

/// <summary>
/// Tests für den <see cref="JsonTimerStateStore"/> gegen das echte Dateisystem:
/// Speichern/Laden erhält die Momentaufnahme, fehlende/entfernte Dateien ergeben
/// <c>null</c>.
/// </summary>
public sealed class JsonTimerStateStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public JsonTimerStateStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "LookAwayTimerState", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "timer-state.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // bereits entfernt
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    [Fact]
    public void Save_und_Load_erhalten_die_Momentaufnahme()
    {
        JsonTimerStateStore store = new(_filePath);
        DateTimeOffset marker = new(2026, 6, 30, 8, 0, 0, TimeSpan.Zero);

        store.Save(new TimerSnapshot(BreakModel.Ultradian, TimeSpan.FromMinutes(42), marker));
        TimerSnapshot? loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(BreakModel.Ultradian, loaded!.Model);
        Assert.Equal(TimeSpan.FromMinutes(42), loaded.WorkRemaining);
        Assert.Equal(marker, loaded.SessionMarker);
    }

    [Fact]
    public void Load_ohne_Datei_liefert_null()
    {
        Assert.Null(new JsonTimerStateStore(_filePath).Load());
    }

    [Fact]
    public void Clear_entfernt_die_Datei()
    {
        JsonTimerStateStore store = new(_filePath);
        store.Save(new TimerSnapshot(BreakModel.ClassicPomodoro, TimeSpan.FromMinutes(5), DateTimeOffset.UnixEpoch));

        store.Clear();

        Assert.Null(store.Load());
        Assert.False(File.Exists(_filePath));
    }
}
