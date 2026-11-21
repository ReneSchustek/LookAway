using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Data.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Integrationstests für das <see cref="JsonSettingsRepository"/> auf dem
/// echten Dateisystem in einem isolierten Temp-Verzeichnis.
/// </summary>
[Collection(DataDirectoryTestGroup.Name)]
public sealed class JsonSettingsRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsFilePath;

    /// <summary>
    /// Legt für jeden Test ein eigenes Temp-Verzeichnis an, damit Tests sich
    /// nicht gegenseitig beeinflussen.
    /// </summary>
    public JsonSettingsRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "LookAway.IT." + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_tempDirectory);
        _settingsFilePath = Path.Combine(_tempDirectory, "settings.json");
    }

    /// <summary>
    /// Räumt das Temp-Verzeichnis nach dem Test wieder ab.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Fremdprozess hält eine Datei: tolerieren, Temp wird vom OS aufgeräumt.
        }
    }

    [Fact]
    public async Task LoadAsync_WithoutFile_ReturnsDefaultsAndIsFirstRunTrue()
    {
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(settings.IsFirstRun);
        Assert.Equal(Language.German, settings.Language);
        Assert.Equal(Settings.DefaultBreakModel, settings.BreakModel);
        Assert.False(settings.AutoStart);
        Assert.Null(settings.CustomDurations);
    }

    [Fact]
    public async Task SaveAsync_CreatesFileInTargetDirectory()
    {
        using JsonSettingsRepository repo = CreateRepository();
        Settings settings = new() { Language = Language.English };

        await repo.SaveAsync(settings, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(_settingsFilePath));
    }

    [Fact]
    public async Task SaveAsync_CreatesParentDirectoryIfMissing()
    {
        string nestedPath = Path.Combine(_tempDirectory, "nested", "subdir", "settings.json");
        using JsonSettingsRepository repo = new(nestedPath, NullLogger<JsonSettingsRepository>.Instance);

        await repo.SaveAsync(new Settings(), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public async Task RoundTrip_PreservesTheOverlayPicture()
    {
        using JsonSettingsRepository repo = CreateRepository();
        Settings original = new()
        {
            BreakOverlayBackground = OverlayBackground.Image,
            BreakOverlayImagePath = @"C:\Bilder\wald.jpg",
            BreakOverlayImageDimPercent = 70,
        };

        await repo.SaveAsync(original, TestContext.Current.CancellationToken);
        Settings loaded = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(OverlayBackground.Image, loaded.BreakOverlayBackground);
        Assert.Equal(@"C:\Bilder\wald.jpg", loaded.BreakOverlayImagePath);
        Assert.Equal(70, loaded.BreakOverlayImageDimPercent);
    }

    [Fact]
    public async Task RoundTrip_PreservesAllValues()
    {
        using JsonSettingsRepository repo = CreateRepository();
        Settings original = new()
        {
            Language = Language.French,
            BreakModel = BreakModel.Ultradian,
            AutoStart = true,
            CustomDurations = new CustomDurations { WorkMinutes = 90, BreakMinutes = 20 },
        };

        await repo.SaveAsync(original, TestContext.Current.CancellationToken);
        Settings loaded = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsFirstRun);
        Assert.Equal(Language.French, loaded.Language);
        Assert.Equal(BreakModel.Ultradian, loaded.BreakModel);
        Assert.True(loaded.AutoStart);
        Assert.NotNull(loaded.CustomDurations);
        Assert.Equal(90, loaded.CustomDurations.WorkMinutes);
        Assert.Equal(20, loaded.CustomDurations.BreakMinutes);
    }

    [Fact]
    public async Task LoadAsync_WithDeletedFile_ReturnsToFirstRunMode()
    {
        using JsonSettingsRepository repo = CreateRepository();
        await repo.SaveAsync(new Settings { Language = Language.English }, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(_settingsFilePath));

        File.Delete(_settingsFilePath);
        Settings reloaded = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(reloaded.IsFirstRun);
        Assert.Equal(Language.German, reloaded.Language);
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedJson_ReturnsDefaultsWithoutThrowing()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "{ this is : not json ::: }", TestContext.Current.CancellationToken);
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(settings.IsFirstRun);
        Assert.Equal(Language.German, settings.Language);
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedJson_BacksUpOriginalContent()
    {
        const string corrupt = "{ this is : not json ::: }";
        await File.WriteAllTextAsync(_settingsFilePath, corrupt, TestContext.Current.CancellationToken);
        using JsonSettingsRepository repo = CreateRepository();

        _ = await repo.LoadAsync(TestContext.Current.CancellationToken);

        string backupPath = _settingsFilePath + ".corrupt";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(corrupt, await File.ReadAllTextAsync(backupPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_WithInvalidValues_BacksUpOriginalContent()
    {
        const string invalid = """{"language":"Klingon","breakModel":"ClassicPomodoro","autoStart":false}""";
        await File.WriteAllTextAsync(_settingsFilePath, invalid, TestContext.Current.CancellationToken);
        using JsonSettingsRepository repo = CreateRepository();

        _ = await repo.LoadAsync(TestContext.Current.CancellationToken);

        string backupPath = _settingsFilePath + ".corrupt";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(invalid, await File.ReadAllTextAsync(backupPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_WithEmptyFile_ReturnsDefaults()
    {
        await File.WriteAllTextAsync(_settingsFilePath, string.Empty, TestContext.Current.CancellationToken);
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(settings.IsFirstRun);
    }

    [Fact]
    public async Task LoadAsync_WithJsonNullLiteral_ReturnsDefaults()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "null", TestContext.Current.CancellationToken);
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(settings.IsFirstRun);
    }

    [Fact]
    public async Task LoadAsync_WithInvalidEnumValue_ReturnsDefaults()
    {
        // CamelCase-Property-Namen entsprechen den SerializerOptions.
        await File.WriteAllTextAsync(_settingsFilePath, """{"language":"Klingon","breakModel":"ClassicPomodoro","autoStart":false}""", TestContext.Current.CancellationToken);
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(settings.IsFirstRun);
    }

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTempFileOnSuccess()
    {
        using JsonSettingsRepository repo = CreateRepository();
        await repo.SaveAsync(new Settings { Language = Language.English }, TestContext.Current.CancellationToken);

        string tempFile = _settingsFilePath + ".tmp";
        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFileAtomically()
    {
        using JsonSettingsRepository repo = CreateRepository();
        await repo.SaveAsync(new Settings { Language = Language.English }, TestContext.Current.CancellationToken);
        await repo.SaveAsync(new Settings { Language = Language.French }, TestContext.Current.CancellationToken);

        Settings reloaded = await repo.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Language.French, reloaded.Language);
    }

    [Fact]
    public async Task ConcurrentSaves_ProduceValidFileAndDoNotDeadlock()
    {
        using JsonSettingsRepository repo = CreateRepository();
        Language[] languages = [Language.German, Language.English, Language.French];

        IEnumerable<Task> writers = Enumerable.Range(0, 30)
            .Select(i => repo.SaveAsync(new Settings { Language = languages[i % languages.Length] }));

        await Task.WhenAll(writers);

        Settings finalState = await repo.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Contains(finalState.Language, languages);
    }

    [Fact]
    public async Task ConcurrentLoadsAndSaves_DoNotCorruptFile()
    {
        using JsonSettingsRepository repo = CreateRepository();
        await repo.SaveAsync(new Settings { Language = Language.English }, TestContext.Current.CancellationToken);

        List<Task> tasks = [];
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(repo.SaveAsync(new Settings { Language = Language.French, AutoStart = true }, TestContext.Current.CancellationToken));
            tasks.Add(repo.LoadAsync(TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        Settings final = await repo.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(final.IsFirstRun);
        Assert.Equal(Language.French, final.Language);
    }

    [Fact]
    public async Task SaveAsync_AfterDispose_Throws()
    {
        JsonSettingsRepository repo = CreateRepository();
        repo.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => repo.SaveAsync(new Settings(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_RejectsNullLogger()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => new JsonSettingsRepository(_settingsFilePath, null!));
    }

    [Fact]
    public void Constructor_RejectsEmptyPath()
    {
        _ = Assert.Throws<ArgumentException>(
            () => new JsonSettingsRepository(string.Empty, NullLogger<JsonSettingsRepository>.Instance));
    }

    [Fact]
    public void GetDefaultFilePath_PointsIntoAppDataLookAway()
    {
        string path = JsonSettingsRepository.GetDefaultFilePath();

        Assert.EndsWith(Path.Combine("LookAway", "settings.json"), path, StringComparison.Ordinal);
        Assert.Contains(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            path,
            StringComparison.Ordinal);
    }

    private JsonSettingsRepository CreateRepository()
        => new(_settingsFilePath, NullLogger<JsonSettingsRepository>.Instance);

    /// <remarks>
    /// Ein Wert, den der Setter ablehnt, ist ein Zustand der Daten wie unlesbares JSON: Der Inhalt
    /// wird gesichert, und es geht mit Standardwerten weiter.
    /// </remarks>
    [Fact]
    public async Task LoadAsync_WithAValueOutOfRange_BacksUpAndReturnsDefaults()
    {
        const string outOfRange = """{"idleThresholdMinutes":999}""";
        await File.WriteAllTextAsync(_settingsFilePath, outOfRange, TestContext.Current.CancellationToken);
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(settings.IsFirstRun);
        Assert.Equal(outOfRange, await File.ReadAllTextAsync(_settingsFilePath + ".corrupt", TestContext.Current.CancellationToken));
    }

    /// <remarks>Eine unlesbare Datei ist ein Zustand des Rechners; Standardwerte darüber zu schreiben, verlöre die echten.</remarks>
    [Fact]
    public async Task LoadAsync_WhenTheFileIsLocked_Throws()
    {
        using JsonSettingsRepository repo = CreateRepository();
        using LockedFile locked = new(_settingsFilePath, "{}");

        _ = await Assert.ThrowsAnyAsync<IOException>(() => repo.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_WhenAccessIsDenied_Throws()
    {
        // Ein Ordner unter dem Dateinamen lässt sich nicht als Datei öffnen; Windows meldet das als verweigerten Zugriff.
        _ = Directory.CreateDirectory(_settingsFilePath);
        using JsonSettingsRepository repo = CreateRepository();

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repo.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_WhenTheTemporaryFileIsLocked_Throws()
    {
        // Geschrieben wird zuerst in eine Zwischendatei neben dem Ziel; hält sie ein anderer
        // Prozess offen, scheitert schon das Anlegen.
        using JsonSettingsRepository repo = CreateRepository();
        using LockedFile locked = new(_settingsFilePath + ".tmp");

        _ = await Assert.ThrowsAnyAsync<IOException>(() => repo.SaveAsync(new Settings(), TestContext.Current.CancellationToken));
    }

    /// <remarks>Das Ersetzen einer Datei, die ein anderer Prozess offen hält, meldet Windows als verweigerten Zugriff.</remarks>
    [Fact]
    public async Task SaveAsync_WhenTheFileIsLocked_Throws()
    {
        using JsonSettingsRepository repo = CreateRepository();
        using LockedFile locked = new(_settingsFilePath, "{}");

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repo.SaveAsync(new Settings(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_WhenTheFileIsReadOnly_Throws()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "{}", TestContext.Current.CancellationToken);
        File.SetAttributes(_settingsFilePath, FileAttributes.ReadOnly);
        using JsonSettingsRepository repo = CreateRepository();

        try
        {
            _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repo.SaveAsync(new Settings(), TestContext.Current.CancellationToken));
        }
        finally
        {
            File.SetAttributes(_settingsFilePath, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Dispose_Twice_IsHarmless()
    {
        JsonSettingsRepository repo = CreateRepository();
        repo.Dispose();

        Exception? thrown = Record.Exception(repo.Dispose);

        Assert.Null(thrown);
    }
}
