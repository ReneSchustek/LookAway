using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests;

/// <summary>
/// Der Update-Bereich der Einstellungen: Beschriftungen in der aktuellen Sprache, die Meldung
/// „aktuell“ ohne Installationsangebot und eine Installation, die ohne gefundenes Update nichts tut.
/// </summary>
public sealed class SettingsViewModelUpdatesTests : IDisposable
{
    private readonly FakeLocalizationService _texts = new();
    private readonly FakeUpdateInstaller _installer = new();
    private readonly InMemorySettingsRepository _repository = new(new Settings());
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelUpdatesTests()
        => _viewModel = TestSettingsViewModels.Create(
            _repository,
            new FakeUpdateChecker(UpdateInfo.NoUpdate(new Version(1, 0, 0))),
            _installer);

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public void Labels_ComeFromTheCurrentLanguage()
    {
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateEnableLabel), _viewModel.UpdateEnableLabel);
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateAutoLabel), _viewModel.AutoUpdateLabel);
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateAutoHint), _viewModel.AutoUpdateHint);
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateFrequencyLabel), _viewModel.UpdateFrequencyLabel);
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateCheckNow), _viewModel.UpdateCheckNowLabel);
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateDownload), _viewModel.UpdateDownloadLabel);
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateInstallNow), _viewModel.UpdateInstallNowLabel);
    }

    [Fact]
    public async Task CheckForUpdates_WhenUpToDate_SaysSoAndOffersNoInstallation()
    {
        await _viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateUpToDate), _viewModel.UpdateStatusText);
        Assert.False(_viewModel.IsUpdateInstallable);
        Assert.Null(_viewModel.DownloadUri);
        Settings persisted = await _repository.LoadAsync(TestContext.Current.CancellationToken);
        _ = Assert.NotNull(persisted.LastUpdateCheck);
    }

    [Fact]
    public async Task InstallUpdate_WithoutAFoundUpdate_DoesNothing()
    {
        await _viewModel.InstallUpdateCommand.ExecuteAsync(null);

        Assert.Equal(0, _installer.StageCallCount);
    }
}
