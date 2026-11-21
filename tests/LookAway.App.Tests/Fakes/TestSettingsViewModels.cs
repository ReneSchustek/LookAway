using LookAway.App.Services;
using LookAway.App.ViewModels;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests.Fakes;

/// <summary>Die Attrappen hinter einem Test-<see cref="SettingsViewModel"/>, für Prüfungen von außen.</summary>
/// <param name="Repository">Ablage der Einstellungen.</param>
/// <param name="AutoStart">Autostart-Eintrag.</param>
/// <param name="Localization">Texte und Sprachwechsel.</param>
/// <param name="Sound">Erinnerungston der Vorschau.</param>
internal sealed record SettingsFakes(
    InMemorySettingsRepository Repository,
    FakeAutoStartService AutoStart,
    FakeLocalizationService Localization,
    FakeSoundService Sound);

/// <summary>
/// Baut ein <see cref="SettingsViewModel"/> mit allen Bereichen über Attrappen, wie es die
/// Einstellungen im Programm bekommen.
/// </summary>
internal static class TestSettingsViewModels
{
    public const string Version = "1.0.0";

    /// <summary>Erzeugt das ViewModel über der angegebenen Ablage.</summary>
    /// <param name="repository">Ablage der Einstellungen.</param>
    /// <param name="checker">Update-Prüfung; ohne Angabe eine ohne Update.</param>
    /// <param name="installer">Update-Installation; ohne Angabe eine Attrappe.</param>
    /// <returns>Das ViewModel.</returns>
    public static SettingsViewModel Create(
        InMemorySettingsRepository repository,
        IUpdateChecker? checker = null,
        IUpdateInstaller? installer = null)
        => Create(new SettingsFakes(repository, new FakeAutoStartService(), new FakeLocalizationService(), new FakeSoundService()), checker, installer);

    /// <summary>Erzeugt das ViewModel über vorgegebenen Attrappen.</summary>
    /// <param name="fakes">Ablage, Autostart, Texte und Ton.</param>
    /// <param name="checker">Update-Prüfung; ohne Angabe eine ohne Update.</param>
    /// <param name="installer">Update-Installation; ohne Angabe eine Attrappe.</param>
    /// <param name="setupUpdate">Setup-Aktualisierung; ohne Angabe eine portable Kopie.</param>
    /// <returns>Das ViewModel.</returns>
    public static SettingsViewModel Create(
        SettingsFakes fakes,
        IUpdateChecker? checker = null,
        IUpdateInstaller? installer = null,
        ISetupUpdate? setupUpdate = null)
    {
        ArgumentNullException.ThrowIfNull(fakes);

        AutoStartCoordinator coordinator = new(fakes.AutoStart, fakes.Repository, NullLogger<AutoStartCoordinator>.Instance);
        SettingsServices services = new(
            fakes.Repository,
            coordinator,
            fakes.Localization,
            fakes.Sound,
            checker ?? new FakeUpdateChecker(),
            installer ?? new FakeUpdateInstaller(),
            setupUpdate ?? new FakeSetupUpdate());
        return new SettingsViewModel(services, CreateSections(fakes.Localization), NullLogger<SettingsViewModel>.Instance, Version);
    }

    private static SettingsSections CreateSections(FakeLocalizationService localization)
    {
        FakeBreakHistoryRepository history = new();
        FakeClock clock = new(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        StatisticsViewModel statistics = new(
            new StatisticsService(history, clock),
            history,
            new CsvExporter(),
            localization,
            NullLogger<StatisticsViewModel>.Instance);

        return new SettingsSections(
            statistics,
            new BreakModelListViewModel(localization, history),
            new LogViewModel(new FakeLogEntryReader(), localization, clock),
            new WorkTaskListViewModel(new FakeWorkTaskRepository(), history, localization, clock));
    }
}
