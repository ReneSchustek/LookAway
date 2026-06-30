using LookAway.Application.Services;
using LookAway.Application.Statistics;
using LookAway.Application.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Tests.Unit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Unit.Application.ViewModels;

/// <summary>
/// Tests fuer das UI-freie <see cref="SettingsViewModel"/> (BRIEF008): Laden,
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
            statistics,
            NullLogger<SettingsViewModel>.Instance,
            TestVersion);
    }

    [Fact]
    public async Task LoadAsync_uebernimmt_die_persistierten_Werte()
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
    public async Task LoadAsync_uebernimmt_benutzerdefinierte_Dauern()
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
    public async Task Save_schreibt_die_geaenderten_Werte_ins_Repository()
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
    public async Task Save_loest_CloseRequested_aus()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();
        bool closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(closed);
    }

    [Fact]
    public async Task Apply_speichert_ohne_zu_schliessen_und_meldet_die_Einstellungen()
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
    public async Task Cancel_verwirft_Aenderungen_und_stellt_die_Sprache_wieder_her()
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
    public async Task Sprachwechsel_schaltet_die_Lokalisierung_sofort_um()
    {
        using SettingsViewModel viewModel = CreateViewModel(
            out _,
            out _,
            out FakeLocalizationService localization);
        await viewModel.LoadAsync();

        viewModel.SelectLanguage(Language.English);

        Assert.Equal(Language.English, localization.CurrentLanguage);
        Assert.StartsWith("English:", viewModel.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ungueltige_Arbeitsdauer_blockiert_das_Speichern()
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
    public async Task Ungueltige_Overlayfarbe_blockiert_das_Speichern()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.BreakOverlayColor = "kein-hex";

        Assert.False(viewModel.CanPersist);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Overlay_und_Update_Einstellungen_werden_geladen_und_gespeichert()
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
        Assert.Equal("#80123456", viewModel.BreakOverlayColor);
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
    public async Task Gueltige_Arbeitsdauer_erlaubt_das_Speichern()
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
    public async Task Ohne_eigene_Dauern_wird_CustomDurations_geloescht()
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
    public async Task Autostart_aktivieren_synchronisiert_Registry_und_Persistenz()
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
    public async Task PhysicalCounter_begrenzt_die_Arbeitsdauer_auf_den_Modellbereich()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.SelectModel(BreakModel.PhysicalCounter);

        Assert.True(viewModel.HasWorkRange);
        Assert.Equal(30, viewModel.WorkMinMinutes);
        Assert.Equal(45, viewModel.WorkMaxMinutes);
    }

    [Fact]
    public async Task Modellwechsel_setzt_die_Dauern_auf_die_Vorgaben_zurueck()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _, out _, out _);
        await viewModel.LoadAsync();

        viewModel.SelectModel(BreakModel.Ultradian);

        Assert.Equal(90, viewModel.WorkMinutes);
        Assert.Equal(20, viewModel.BreakMinutes);
    }

    [Fact]
    public async Task Sound_Einstellungen_werden_geladen_und_gespeichert()
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
    public async Task Vorhoeren_spielt_den_gewaehlten_Ton_mit_der_Lautstaerke()
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
