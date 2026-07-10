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

        Settings settings = await repo.LoadAsync();

        Assert.True(settings.IsFirstRun);
        Assert.Equal(Language.German, settings.Language);
        Assert.Equal(BreakModel.ClassicPomodoro, settings.BreakModel);
        Assert.False(settings.AutoStart);
        Assert.Null(settings.CustomDurations);
    }

    [Fact]
    public async Task SaveAsync_CreatesFileInTargetDirectory()
    {
        using JsonSettingsRepository repo = CreateRepository();
        Settings settings = new() { Language = Language.English };

        await repo.SaveAsync(settings);

        Assert.True(File.Exists(_settingsFilePath));
    }

    [Fact]
    public async Task SaveAsync_CreatesParentDirectoryIfMissing()
    {
        string nestedPath = Path.Combine(_tempDirectory, "nested", "subdir", "settings.json");
        using JsonSettingsRepository repo = new(nestedPath, NullLogger<JsonSettingsRepository>.Instance);

        await repo.SaveAsync(new Settings());

        Assert.True(File.Exists(nestedPath));
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

        await repo.SaveAsync(original);
        Settings loaded = await repo.LoadAsync();

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
        await repo.SaveAsync(new Settings { Language = Language.English });
        Assert.True(File.Exists(_settingsFilePath));

        File.Delete(_settingsFilePath);
        Settings reloaded = await repo.LoadAsync();

        Assert.True(reloaded.IsFirstRun);
        Assert.Equal(Language.German, reloaded.Language);
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedJson_ReturnsDefaultsWithoutThrowing()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "{ this is : not json ::: }");
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync();

        Assert.True(settings.IsFirstRun);
        Assert.Equal(Language.German, settings.Language);
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedJson_BacksUpOriginalContent()
    {
        const string corrupt = "{ this is : not json ::: }";
        await File.WriteAllTextAsync(_settingsFilePath, corrupt);
        using JsonSettingsRepository repo = CreateRepository();

        _ = await repo.LoadAsync();

        string backupPath = _settingsFilePath + ".corrupt";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(corrupt, await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task LoadAsync_WithInvalidValues_BacksUpOriginalContent()
    {
        const string invalid = """{"language":"Klingon","breakModel":"ClassicPomodoro","autoStart":false}""";
        await File.WriteAllTextAsync(_settingsFilePath, invalid);
        using JsonSettingsRepository repo = CreateRepository();

        _ = await repo.LoadAsync();

        string backupPath = _settingsFilePath + ".corrupt";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(invalid, await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task LoadAsync_WithEmptyFile_ReturnsDefaults()
    {
        await File.WriteAllTextAsync(_settingsFilePath, string.Empty);
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync();

        Assert.True(settings.IsFirstRun);
    }

    [Fact]
    public async Task LoadAsync_WithJsonNullLiteral_ReturnsDefaults()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "null");
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync();

        Assert.True(settings.IsFirstRun);
    }

    [Fact]
    public async Task LoadAsync_WithInvalidEnumValue_ReturnsDefaults()
    {
        // CamelCase-Property-Namen entsprechen den SerializerOptions.
        await File.WriteAllTextAsync(_settingsFilePath, """{"language":"Klingon","breakModel":"ClassicPomodoro","autoStart":false}""");
        using JsonSettingsRepository repo = CreateRepository();

        Settings settings = await repo.LoadAsync();

        Assert.True(settings.IsFirstRun);
    }

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTempFileOnSuccess()
    {
        using JsonSettingsRepository repo = CreateRepository();
        await repo.SaveAsync(new Settings { Language = Language.English });

        string tempFile = _settingsFilePath + ".tmp";
        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFileAtomically()
    {
        using JsonSettingsRepository repo = CreateRepository();
        await repo.SaveAsync(new Settings { Language = Language.English });
        await repo.SaveAsync(new Settings { Language = Language.French });

        Settings reloaded = await repo.LoadAsync();
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

        Settings finalState = await repo.LoadAsync();
        Assert.Contains(finalState.Language, languages);
    }

    [Fact]
    public async Task ConcurrentLoadsAndSaves_DoNotCorruptFile()
    {
        using JsonSettingsRepository repo = CreateRepository();
        await repo.SaveAsync(new Settings { Language = Language.English });

        List<Task> tasks = [];
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(repo.SaveAsync(new Settings { Language = Language.French, AutoStart = true }));
            tasks.Add(repo.LoadAsync());
        }

        await Task.WhenAll(tasks);

        Settings final = await repo.LoadAsync();
        Assert.False(final.IsFirstRun);
        Assert.Equal(Language.French, final.Language);
    }

    [Fact]
    public async Task SaveAsync_AfterDispose_Throws()
    {
        JsonSettingsRepository repo = CreateRepository();
        repo.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => repo.SaveAsync(new Settings()));
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
}
