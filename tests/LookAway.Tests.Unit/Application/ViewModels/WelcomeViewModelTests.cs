using LookAway.Application.Services;
using LookAway.Application.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Tests.Unit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Unit.Application.ViewModels;

/// <summary>
/// Tests fuer die UI-freie State-Machine des First-Run-Wizards
/// <see cref="WelcomeViewModel"/> (BRIEF009).
/// </summary>
public sealed class WelcomeViewModelTests
{
    private static WelcomeViewModel CreateViewModel(
        out InMemorySettingsRepository repository,
        out FakeAutoStartService autoStart,
        out FakeLocalizationService localization,
        Language detected = Language.German)
    {
        repository = new InMemorySettingsRepository();
        autoStart = new FakeAutoStartService();
        localization = new FakeLocalizationService();

        AutoStartCoordinator coordinator = new(
            autoStart,
            repository,
            NullLogger<AutoStartCoordinator>.Instance);

        return new WelcomeViewModel(
            repository,
            coordinator,
            localization,
            NullLogger<WelcomeViewModel>.Instance,
            detected);
    }

    [Fact]
    public void Start_beginnt_im_ersten_Schritt_mit_Vorgaben()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _, Language.French);

        Assert.Equal(0, viewModel.CurrentStep);
        Assert.True(viewModel.IsFirstStep);
        Assert.False(viewModel.IsLastStep);
        Assert.Equal(Language.French, viewModel.SelectedLanguage);
        Assert.Equal(WelcomeViewModel.DefaultModel, viewModel.SelectedModel);
        Assert.True(viewModel.AutoStart);
    }

    [Fact]
    public void Next_und_Back_navigieren_durch_die_Schritte()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);

        Assert.False(viewModel.BackCommand.CanExecute(null));

        viewModel.NextCommand.Execute(null);
        Assert.Equal(1, viewModel.CurrentStep);

        viewModel.NextCommand.Execute(null);
        Assert.Equal(2, viewModel.CurrentStep);
        Assert.True(viewModel.IsLastStep);
        Assert.False(viewModel.NextCommand.CanExecute(null));

        viewModel.BackCommand.Execute(null);
        Assert.Equal(1, viewModel.CurrentStep);
    }

    [Fact]
    public void Finish_ist_nur_im_letzten_Schritt_ausfuehrbar()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);

        Assert.False(viewModel.FinishCommand.CanExecute(null));

        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);

        Assert.True(viewModel.FinishCommand.CanExecute(null));
    }

    [Fact]
    public async Task Finish_persistiert_Konfiguration_und_meldet_Abschluss()
    {
        using WelcomeViewModel viewModel = CreateViewModel(
            out InMemorySettingsRepository repository,
            out FakeAutoStartService autoStart,
            out _,
            Language.English);
        Settings? completed = null;
        viewModel.Completed += (_, e) => completed = e.Settings;

        viewModel.SelectedModelOption = viewModel.Models.First(o => o.Value == BreakModel.Ultradian);
        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);
        viewModel.AutoStart = true;
        await viewModel.FinishCommand.ExecuteAsync(null);

        Assert.NotNull(completed);
        Settings persisted = await repository.LoadAsync();
        Assert.Equal(Language.English, persisted.Language);
        Assert.Equal(BreakModel.Ultradian, persisted.BreakModel);
        Assert.True(autoStart.IsEnabled());
    }

    [Fact]
    public void Sprachwechsel_schaltet_die_Lokalisierung_sofort_um()
    {
        using WelcomeViewModel viewModel = CreateViewModel(
            out _,
            out _,
            out FakeLocalizationService localization,
            Language.German);

        viewModel.SelectedLanguageOption = viewModel.Languages.First(o => o.Value == Language.French);

        Assert.Equal(Language.French, localization.CurrentLanguage);
    }
}
