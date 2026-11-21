using System.Globalization;
using LookAway.Core.Interfaces;
using LookAway.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AutoStartCoordinator = LookAway.Core.Services.AutoStartCoordinator;
using SettingsViewModel = LookAway.App.ViewModels.SettingsViewModel;
using StatisticsViewModel = LookAway.App.ViewModels.StatisticsViewModel;
using BreakModelListViewModel = LookAway.App.ViewModels.BreakModelListViewModel;
using LogViewModel = LookAway.App.ViewModels.LogViewModel;
using SettingsSections = LookAway.App.ViewModels.SettingsSections;
using SettingsServices = LookAway.App.ViewModels.SettingsServices;
using WorkTaskListViewModel = LookAway.App.ViewModels.WorkTaskListViewModel;
using WelcomeViewModel = LookAway.App.ViewModels.WelcomeViewModel;
using StatisticsService = LookAway.Core.Services.StatisticsService;
using CsvExporter = LookAway.Core.Services.CsvExporter;
using UpdateInstallerService = LookAway.Data.Update.UpdateInstallerService;
using IUpdateInstaller = LookAway.Core.Interfaces.IUpdateInstaller;

namespace LookAway.App;

/// <summary>
/// Fabriken der ViewModels für Wizard und Einstellungen, verdrahtet mit den Diensten des Containers.
/// </summary>
public sealed partial class LookAwayApp
{
    private UpdateOrchestrator CreateUpdateOrchestrator()
    {
        UpdateOrchestrator orchestrator = new(
            Services.GetRequiredService<UpdateInstallerService>(),
            Services.GetRequiredService<IUpdateChecker>(),
            Services.GetRequiredService<ISettingsRepository>(),
            Services.GetRequiredService<IClock>(),
            Services.GetRequiredService<IUpdateProcess>(),
            ParseVersion(GetVersion()),
            Services.GetRequiredService<ILogger<UpdateOrchestrator>>())
        {
            UpdateAvailableChanged = available => _trayIcon?.SetUpdateAvailable(available),
            RelaunchRequested = RequestExit,
        };
        return orchestrator;
    }

    private WelcomeViewModel CreateWelcomeViewModel() => new(
        Services.GetRequiredService<ISettingsRepository>(),
        Services.GetRequiredService<AutoStartCoordinator>(),
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<ILogger<WelcomeViewModel>>(),
        SystemLanguage.FromCulture(CultureInfo.CurrentUICulture));

    private SettingsViewModel CreateSettingsViewModel() => new(
        new SettingsServices(
            Services.GetRequiredService<ISettingsRepository>(),
            Services.GetRequiredService<AutoStartCoordinator>(),
            Services.GetRequiredService<ILocalizationService>(),
            Services.GetRequiredService<ISoundService>(),
            Services.GetRequiredService<IUpdateChecker>(),
            Services.GetRequiredService<IUpdateInstaller>(),
            Services.GetRequiredService<ISetupUpdate>()),
        new SettingsSections(
            CreateStatisticsViewModel(),
            CreateBreakModelListViewModel(),
            CreateLogViewModel(),
            CreateWorkTaskListViewModel()),
        Services.GetRequiredService<ILogger<SettingsViewModel>>(),
        GetVersion());

    private StatisticsViewModel CreateStatisticsViewModel() => new(
        Services.GetRequiredService<StatisticsService>(),
        Services.GetRequiredService<IBreakHistoryRepository>(),
        Services.GetRequiredService<CsvExporter>(),
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<ILogger<StatisticsViewModel>>());

    private BreakModelListViewModel CreateBreakModelListViewModel() => new(
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<IBreakHistoryRepository>());

    private WorkTaskListViewModel CreateWorkTaskListViewModel() => new(
        Services.GetRequiredService<IWorkTaskRepository>(),
        Services.GetRequiredService<IBreakHistoryRepository>(),
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<IClock>());

    private LogViewModel CreateLogViewModel() => new(
        Services.GetRequiredService<ILogEntryReader>(),
        Services.GetRequiredService<ILocalizationService>(),
        Services.GetRequiredService<IClock>());
}
