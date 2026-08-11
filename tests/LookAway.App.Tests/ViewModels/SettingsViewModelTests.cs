using LookAway.Core.Services;
using LookAway.Core.ValueObjects;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.App.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Tests für das UI-freie <see cref="SettingsViewModel"/>: Laden,
/// Validieren, Persistieren, Sprachvorschau und Autostart-Synchronisation.
/// </summary>
public sealed class SettingsViewModelTests
{
    private const string TestVersion = "1.0.0";

    private static SettingsViewModel CreateViewModel(
        out InMemorySettingsRepository repository,
        out FakeAutoStartService autoStart,
        out FakeLocalizationService localization,
        Settings? initial = null)
        => CreateViewModel(out repository, out autoStart, out localization, out _, initial);

    private static SettingsViewModel CreateViewModel(
        out InMemorySettingsRepository repository,
        out FakeAutoStartService autoStart,
        out FakeLocalizationService localization,
        out FakeSoundService sound,
        Settings? initial = null)
    {
        repository = new InMemorySettingsRepository(initial);
        autoStart = new FakeAutoStartService();
        localization = new FakeLocalizationService();
        sound = new FakeSoundService();

        AutoStartCoordinator coordinator = new(
            autoStart,
            repository,
            NullLogger<AutoStartCoordinator>.Instance);

        FakeBreakHistoryRepository history = new();
        FakeClock clock = new(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        StatisticsViewModel statistics = new(
            new StatisticsService(history, clock),
            history,
            new CsvExporter(),
            localization);

        return new SettingsViewModel(
            repository,
            coordinator,
            localization,
            sound,
            new FakeUpdateChecker(),
            new FakeUpdateInstaller(),
            new SettingsSections(
                statistics,
                new BreakModelListViewModel(localization, history),
                new LogViewModel(new FakeLogEntryReader(), localization, clock),
                new WorkTaskListViewModel(new FakeWorkTaskRepository(), history, localization, clock)),
            NullLogger<SettingsViewModel>.Instance,
            TestVersion);
    }

    // Variante mit gezielt vorgegebenem Update-Checker/-Installer für die Update-Tests.
    private static SettingsViewModel CreateViewModelWithUpdates(
        IUpdateChecker checker,
        IUpdateInstaller installer,
        out InMemorySettingsRepository repository)
    {
        repository = new InMemorySettingsRepository(null);
        FakeLocalizationService localization = new();
        AutoStartCoordinator coordinator = new(
            new FakeAutoStartService(),
            repository,
            NullLogger<AutoStartCoordinator>.Instance);
        FakeBreakHistoryRepository history = new();
        FakeClock clock = new(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        StatisticsViewModel statistics = new(
            new StatisticsService(history, clock),
            history,
            new CsvExporter(),
            localization);

        return new SettingsViewModel(
            repository,
            coordinator,
            localization,
            new FakeSoundService(),
            checker,
            installer,
            new SettingsSections(
                statistics,
                new BreakModelListViewModel(localization, history),
                new LogViewModel(new FakeLogEntryReader(), localization, clock),
                new WorkTaskListViewModel(new FakeWorkTaskRepository(), history, localization, clock)),
            NullLogger<SettingsViewModel>.Instance,
            TestVersion);
    }

    [Fact]
    public async Task CheckForUpdates_WithAnAvailablePackage_OffersInstallation()
    {
        UpdateInfo info = UpdateInfo.Create(
            new Version(1, 0, 0), "v2.0.0", "https://example.com/r", null,
            "https://example.com/p.zip", "https://example.com/p.zip.sig");
        using SettingsViewModel viewModel = CreateViewModelWithUpdates(
            new FakeUpdateChecker(info), new FakeUpdateInstaller(), out _);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsUpdateInstallable);
    }

    [Fact]
    public async Task InstallUpdate_StagesAndRecordsThePendingVersion()
    {
        UpdateInfo info = UpdateInfo.Create(
            new Version(1, 0, 0), "v2.0.0", null, null,
            "https://example.com/p.zip", "https://example.com/p.zip.sig");
        StagedUpdate staged = new("C:/staging/2.0.0", "2.0.0", "abc123");
        FakeUpdateInstaller installer = new(staged);
        using SettingsViewModel viewModel = CreateViewModelWithUpdates(
            new FakeUpdateChecker(info), installer, out InMemorySettingsRepository repository);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        await viewModel.InstallUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, installer.StageCallCount);
        Settings saved = await repository.LoadAsync();
        Assert.Equal("2.0.0", saved.PendingUpdateVersion);
        Assert.Equal("abc123", saved.PendingUpdateSha256);
        // Nach erfolgreichem Staging ist der Button ausgeblendet (kein Doppelklick).
        Assert.False(viewModel.IsUpdateInstallable);
    }

    [Fact]
    public async Task InstallUpdate_OnFailure_OffersAgain()
    {
        UpdateInfo info = UpdateInfo.Create(
            new Version(1, 0, 0), "v2.0.0", null, null,
            "https://example.com/p.zip", "https://example.com/p.zip.sig");
        // Installer ohne Ergebnis -> Staging schlägt fehl.
        using SettingsViewModel viewModel = CreateViewModelWithUpdates(
            new FakeUpdateChecker(info), new FakeUpdateInstaller(result: null), out _);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        await viewModel.InstallUpdateCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsUpdateInstallable);
    }

    [Fact]
    public async Task LoadAsync_TakesThePersistedValues()
    {
        Settings stored = new()
        {
            Language = Language.French,
            BreakModel = BreakModel.Ultradian,
            AutoStart = true,
        };
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _, stored);

        await viewModel.LoadAsync();

        Assert.Equal(Language.French, viewModel.SelectedLanguage);
        Assert.Equal(BreakModel.Ultradian, viewModel.SelectedModel);
        Assert.True(viewModel.AutoStart);
        Assert.False(viewModel.UseCustomDurations);
    }

    [Fact]
    public async Task LoadAsync_TakesTheCustomDurations()
    {
        Settings stored = new()
        {
            BreakModel = BreakModel.ClassicPomodoro,
            CustomDurations = new CustomDurations { WorkMinutes = 30, BreakMinutes = 7 },
        };
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _, stored);

        await viewModel.LoadAsync();

        Assert.True(viewModel.UseCustomDurations);
        Assert.Equal(30, viewModel.WorkMinutes);
        Assert.Equal(7, viewModel.BreakMinutes);
    }

    [Fact]
    public async Task Save_WritesTheChangedValuesToTheRepository()
    {
        using SettingsViewModel viewModel = CreateViewModel(out InMemorySettingsRepository repository, out _, out _);
        await viewModel.LoadAsync();

        viewModel.SelectModel(BreakModel.LegalCompliance);
        viewModel.SelectLanguage(Language.English);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Settings persisted = await repository.LoadAsync();
        Assert.Equal(BreakModel.LegalCompliance, persisted.BreakModel);
        Assert.Equal(Language.English, persisted.Language);
    }

    [Fact]
    public async Task Save_RaisesCloseRequested()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();
        bool closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(closed);
    }

    [Fact]
    public async Task Apply_SavesWithoutClosingAndReportsTheSettings()
    {
        using SettingsViewModel viewModel = CreateViewModel(out InMemorySettingsRepository repository, out _, out _);
        await viewModel.LoadAsync();
        bool closed = false;
        Settings? applied = null;
        viewModel.CloseRequested += (_, _) => closed = true;
        viewModel.SettingsApplied += (_, e) => applied = e.Settings;

        viewModel.SelectModel(BreakModel.ShortBreaks);
        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.NotNull(applied);
        Assert.Equal(BreakModel.ShortBreaks, applied!.BreakModel);
        Assert.True(repository.SaveCallCount >= 1);
    }

    [Fact]
    public async Task Cancel_DiscardsChangesAndRestoresTheLanguage()
    {
        using SettingsViewModel viewModel = CreateViewModel(
            out InMemorySettingsRepository repository,
            out _,
            out FakeLocalizationService localization);
        await viewModel.LoadAsync();
        int savesAfterLoad = repository.SaveCallCount;
        bool closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        viewModel.SelectLanguage(Language.English);
        viewModel.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(Language.German, localization.CurrentLanguage);
        Assert.Equal(savesAfterLoad, repository.SaveCallCount);
    }

    [Fact]
    public async Task LanguageChange_SwitchesLocalizationImmediately()
    {
        using SettingsViewModel viewModel = CreateViewModel(
            out _,
            out _,
            out FakeLocalizationService localization);
        await viewModel.LoadAsync();

        viewModel.SelectLanguage(Language.English);

        Assert.Equal(Language.English, localization.CurrentLanguage);
        Assert.StartsWith("English:", viewModel.Texts.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidWorkDuration_BlocksSaving()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.UseCustomDurations = true;
        viewModel.WorkMinutes = 2; // unter dem Minimum von 5

        Assert.NotNull(viewModel.WorkError);
        Assert.False(viewModel.CanPersist);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task InvalidOverlayColor_BlocksSaving()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.BreakOverlayColor = "kein-hex";

        Assert.False(viewModel.CanPersist);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task OverlayAndUpdateSettings_AreLoadedAndSaved()
    {
        Settings stored = new()
        {
            DarkenAllScreens = false,
            BreakOverlayColor = "#80123456",
            DimScreenDuringBreak = true,
            DimBrightnessPercent = 50,
            PauseMediaDuringBreak = true,
            AutoUpdate = true,
            UpdateCheckEnabled = false,
        };
        SettingsViewModel viewModel = CreateViewModel(out InMemorySettingsRepository repository, out _, out _, stored);
        await viewModel.LoadAsync();

        Assert.False(viewModel.DarkenAllScreens);
        // Halbtransparent gespeicherte Farbe wird beim Laden auf ihr deckendes
        // Äquivalent (über Weiß zusammengesetzt) migriert — Transparenz entfällt.
        Assert.Equal("#FF8899AA", viewModel.BreakOverlayColor);
        Assert.True(viewModel.DimScreenDuringBreak);
        Assert.Equal(50, viewModel.DimBrightnessPercent);
        Assert.True(viewModel.AutoUpdate);
        Assert.False(viewModel.UpdateCheckEnabled);

        viewModel.DarkenAllScreens = true;
        viewModel.BreakOverlayColor = "#FF222222";
        viewModel.AutoUpdate = false;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Settings saved = await repository.LoadAsync();
        Assert.True(saved.DarkenAllScreens);
        Assert.Equal("#FF222222", saved.BreakOverlayColor);
        Assert.False(saved.AutoUpdate);
        viewModel.Dispose();
    }

    [Fact]
    public async Task ValidWorkDuration_AllowsSaving()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.UseCustomDurations = true;
        viewModel.WorkMinutes = 25;
        viewModel.BreakMinutes = 5;

        Assert.Null(viewModel.WorkError);
        Assert.Null(viewModel.BreakError);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task WithoutCustomDurations_TheOverrideIsCleared()
    {
        Settings stored = new()
        {
            CustomDurations = new CustomDurations { WorkMinutes = 30, BreakMinutes = 7 },
        };
        using SettingsViewModel viewModel = CreateViewModel(out InMemorySettingsRepository repository, out _, out _, stored);
        await viewModel.LoadAsync();

        viewModel.UseCustomDurations = false;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Settings persisted = await repository.LoadAsync();
        Assert.Null(persisted.CustomDurations);
    }

    [Fact]
    public async Task EnablingAutoStart_SynchronizesRegistryAndSettings()
    {
        using SettingsViewModel viewModel = CreateViewModel(
            out InMemorySettingsRepository repository,
            out FakeAutoStartService autoStart,
            out _);
        await viewModel.LoadAsync();

        viewModel.AutoStart = true;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(autoStart.IsEnabled());
        Settings persisted = await repository.LoadAsync();
        Assert.True(persisted.AutoStart);
    }

    [Fact]
    public async Task PhysicalCounter_LimitsWorkDurationToTheModelRange()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.SelectModel(BreakModel.PhysicalCounter);

        Assert.True(viewModel.HasWorkRange);
        Assert.Equal(30, viewModel.WorkMinMinutes);
        Assert.Equal(45, viewModel.WorkMaxMinutes);
    }

    [Fact]
    public async Task ModelChange_ResetsTheDurationsToTheDefaults()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.SelectModel(BreakModel.Ultradian);

        Assert.Equal(90, viewModel.WorkMinutes);
        Assert.Equal(20, viewModel.BreakMinutes);
    }

    [Fact]
    public async Task SoundSettings_AreLoadedAndSaved()
    {
        Settings stored = new()
        {
            SoundEnabled = true,
            ReminderSound = SoundType.Bell,
            SoundVolumePercent = 55,
        };
        using SettingsViewModel viewModel = CreateViewModel(
            out InMemorySettingsRepository repository, out _, out _, out _, stored);
        await viewModel.LoadAsync();

        Assert.True(viewModel.SoundEnabled);
        Assert.Equal(SoundType.Bell, viewModel.SelectedSound);
        Assert.Equal(55, viewModel.SoundVolume);

        viewModel.SelectSound(SoundType.Pop);
        viewModel.SoundVolume = 40;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Settings persisted = await repository.LoadAsync();
        Assert.True(persisted.SoundEnabled);
        Assert.Equal(SoundType.Pop, persisted.ReminderSound);
        Assert.Equal(40, persisted.SoundVolumePercent);
    }

    [Fact]
    public async Task Preview_PlaysTheChosenSoundAtTheSetVolume()
    {
        using SettingsViewModel viewModel = CreateViewModel(
            out _, out _, out _, out FakeSoundService sound);
        await viewModel.LoadAsync();

        viewModel.SelectSound(SoundType.Bell);
        viewModel.SoundVolume = 70;
        viewModel.PreviewSoundCommand.Execute(null);

        Assert.Equal(1, sound.PlayCallCount);
        Assert.Equal(SoundType.Bell, sound.LastSound);
        Assert.Equal(70, sound.LastVolume);
    }
}
