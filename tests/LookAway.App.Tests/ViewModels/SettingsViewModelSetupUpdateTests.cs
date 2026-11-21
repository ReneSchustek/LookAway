using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests;

/// <summary>
/// Die Update-Seite einer installierten Kopie: Sie bietet die Installation nur mit signiertem Setup
/// an, startet es auf Klick und sagt, dass Windows nun nach Administratorrechten fragt.
/// </summary>
public sealed class SettingsViewModelSetupUpdateTests : IDisposable
{
    private static readonly UpdateInfo WithSetup = UpdateInfo.Create(
        new Version(1, 0, 0), "v2.0.0", "https://example.com/r", null,
        new ReleaseAssets("https://example.com/p.zip", "https://example.com/p.zip.sig", "https://example.com/s.exe", "https://example.com/s.exe.sig"));

    private readonly FakeLocalizationService _texts = new();
    private readonly FakeSetupUpdate _setup = new() { IsActive = true };
    private readonly FakeUpdateInstaller _installer = new();
    private SettingsViewModel? _viewModel;

    public void Dispose() => _viewModel?.Dispose();

    [Fact]
    public async Task FoundUpdate_WithASignedSetup_IsOfferedForInstallation()
    {
        SettingsViewModel viewModel = Create(WithSetup);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsUpdateInstallable);
    }

    [Fact]
    public async Task FoundUpdate_WithoutSetup_IsNotOffered()
    {
        SettingsViewModel viewModel = Create(UpdateInfo.Create(
            new Version(1, 0, 0), "v2.0.0", "https://example.com/r", null, new ReleaseAssets("https://example.com/p.zip", "https://example.com/p.zip.sig")));

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsUpdateInstallable);
    }

    [Fact]
    public async Task Install_StartsTheSetupAndSaysWhatHappensNext()
    {
        SettingsViewModel viewModel = Create(WithSetup);
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        await viewModel.InstallUpdateCommand.ExecuteAsync(null);

        Assert.Same(WithSetup, Assert.Single(_setup.Started));
        Assert.Equal(0, _installer.StageCallCount); // kein ZIP
        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateSetupStarted), viewModel.UpdateStatusText);
        Assert.False(viewModel.IsUpdateInstallable);
    }

    [Fact]
    public async Task Install_WhenTheSetupDoesNotStart_OffersItAgain()
    {
        _setup.StartSucceeds = false;
        SettingsViewModel viewModel = Create(WithSetup);
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        await viewModel.InstallUpdateCommand.ExecuteAsync(null);

        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateInstallFailed), viewModel.UpdateStatusText);
        Assert.True(viewModel.IsUpdateInstallable);
    }

    [Fact]
    public void AutoUpdateHint_SaysThatInstallingWaitsForAClick()
    {
        SettingsViewModel viewModel = Create(WithSetup);

        Assert.Equal(_texts.GetText(SettingsTextKeys.UpdateAutoHintSetup), viewModel.AutoUpdateHint);
    }

    private SettingsViewModel Create(UpdateInfo found)
    {
        SettingsFakes fakes = new(new InMemorySettingsRepository(new Settings()), new FakeAutoStartService(), _texts, new FakeSoundService());
        _viewModel = TestSettingsViewModels.Create(fakes, new FakeUpdateChecker(found), _installer, _setup);
        return _viewModel;
    }
}
