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
    public void SaveAndLoad_PreserveTheSnapshot()
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
    public void Load_WithoutFile_ReturnsNull()
    {
        Assert.Null(new JsonTimerStateStore(_filePath).Load());
    }

    [Fact]
    public void Clear_RemovesTheFile()
    {
        JsonTimerStateStore store = new(_filePath);
        store.Save(new TimerSnapshot(BreakModel.ClassicPomodoro, TimeSpan.FromMinutes(5), DateTimeOffset.UnixEpoch));

        store.Clear();

        Assert.Null(store.Load());
        Assert.False(File.Exists(_filePath));
    }

    /// <remarks>
    /// Eine halb geschriebene Datei — etwa nach einem Stromausfall mitten im Sichern —
    /// darf den Start nicht aufhalten. Der Timer beginnt dann eben mit voller
    /// Arbeitsdauer.
    /// </remarks>
    [Theory]
    [InlineData("{ das ist kein JSON")]
    [InlineData("{\"model\":")]
    [InlineData("[]")]
    public void Load_WithBrokenContent_ReturnsNull(string content)
    {
        File.WriteAllText(_filePath, content);

        Assert.Null(new JsonTimerStateStore(_filePath).Load());
    }

    [Fact]
    public void Load_WithEmptyFile_ReturnsNull()
    {
        File.WriteAllText(_filePath, "   ");

        Assert.Null(new JsonTimerStateStore(_filePath).Load());
    }

    /// <remarks>
    /// Die Datei kann gerade von einem Virenscanner oder einer zweiten Instanz
    /// gehalten werden. Auch dann startet die Anwendung.
    /// </remarks>
    [Fact]
    public void Load_WithLockedFile_ReturnsNull()
    {
        using LockedFile locked = new(_filePath, "{}");

        Assert.Null(new JsonTimerStateStore(_filePath).Load());
    }

    [Fact]
    public void Save_WithLockedFile_IsIgnored()
    {
        using LockedFile locked = new(_filePath);
        JsonTimerStateStore store = new(_filePath);

        Exception? thrown = Record.Exception(
            () => store.Save(new TimerSnapshot(BreakModel.Ultradian, TimeSpan.FromMinutes(3), DateTimeOffset.UnixEpoch)));

        Assert.Null(thrown);
    }

    [Fact]
    public void Clear_WithLockedFile_IsIgnored()
    {
        using LockedFile locked = new(_filePath, "{}");
        JsonTimerStateStore store = new(_filePath);

        Exception? thrown = Record.Exception(store.Clear);

        Assert.Null(thrown);
    }

    /// <remarks>
    /// Zeigt der Pfad auf ein Verzeichnis, verweigert Windows den Schreibzugriff mit
    /// einer anderen Ausnahme als bei der gesperrten Datei — beide Zweige müssen
    /// schweigen.
    /// </remarks>
    [Fact]
    public void Save_WhenPathIsADirectory_IsIgnored()
    {
        string asDirectory = Path.Combine(_directory, "belegt");
        _ = Directory.CreateDirectory(asDirectory);
        JsonTimerStateStore store = new(asDirectory);

        Exception? thrown = Record.Exception(
            () => store.Save(new TimerSnapshot(BreakModel.ClassicPomodoro, TimeSpan.FromMinutes(1), DateTimeOffset.UnixEpoch)));

        Assert.Null(thrown);
    }
}
