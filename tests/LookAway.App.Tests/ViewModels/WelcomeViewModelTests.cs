using LookAway.Core.Services;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.App.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Tests für die UI-freie State-Machine des First-Run-Wizards
/// <see cref="WelcomeViewModel"/>.
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
    public void Start_BeginsAtTheFirstStepWithDefaults()
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
    public void NextAndBack_MoveThroughTheSteps()
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
    public void Finish_IsOnlyAvailableOnTheLastStep()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);

        Assert.False(viewModel.FinishCommand.CanExecute(null));

        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);

        Assert.True(viewModel.FinishCommand.CanExecute(null));
    }

    [Fact]
    public async Task Finish_PersistsTheConfigurationAndReportsCompletion()
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
    public void LanguageChange_SwitchesLocalizationImmediately()
    {
        using WelcomeViewModel viewModel = CreateViewModel(
            out _,
            out _,
            out FakeLocalizationService localization,
            Language.German);

        viewModel.SelectedLanguageOption = viewModel.Languages.First(o => o.Value == Language.French);

        Assert.Equal(Language.French, localization.CurrentLanguage);
    }

    /// <remarks>
    /// Der Assistent zeigt je Schritt genau eine Seite und dazu die Schaltflächen, die
    /// dort sinnvoll sind: kein „Zurück" auf der ersten, kein „Weiter" auf der letzten.
    /// Stimmt das nicht, sitzt der Nutzer beim ersten Start in einer Sackgasse.
    /// </remarks>
    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(1, false, true, false)]
    [InlineData(2, false, false, true)]
    public void EachStep_ShowsExactlyOnePage(int step, bool language, bool model, bool autoStart)
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);

        for (int i = 0; i < step; i++)
        {
            viewModel.NextCommand.Execute(null);
        }

        Assert.Equal(language, viewModel.IsStepLanguage);
        Assert.Equal(model, viewModel.IsStepModel);
        Assert.Equal(autoStart, viewModel.IsStepAutoStart);
    }

    [Fact]
    public void FirstStep_HasNoBackButtonAndNoFinish()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);

        Assert.False(viewModel.ShowBackButton);
        Assert.True(viewModel.ShowNextButton);
        Assert.False(viewModel.ShowFinishButton);
    }

    [Fact]
    public void LastStep_OffersFinishInsteadOfNext()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);
        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);

        Assert.True(viewModel.ShowBackButton);
        Assert.False(viewModel.ShowNextButton);
        Assert.True(viewModel.ShowFinishButton);
    }

    /// <remarks>
    /// Jeder Schritt bringt seine eigene Überschrift mit; zwei gleiche wären ein
    /// vergessener Schlüssel.
    /// </remarks>
    [Fact]
    public void EveryStep_HasItsOwnHeader()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);
        List<string> headers = [viewModel.StepHeader];

        viewModel.NextCommand.Execute(null);
        headers.Add(viewModel.StepHeader);
        viewModel.NextCommand.Execute(null);
        headers.Add(viewModel.StepHeader);

        Assert.Equal(3, headers.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryLabelReturnsText()
    {
        using WelcomeViewModel viewModel = CreateViewModel(out _, out _, out _);
        List<string> empty = [];

        foreach (System.Reflection.PropertyInfo property in typeof(WelcomeViewModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead))
        {
            if (property.GetValue(viewModel) is not string value || string.IsNullOrWhiteSpace(value))
            {
                empty.Add(property.Name);
            }
        }

        Assert.True(empty.Count == 0, "Ohne Text: " + string.Join(", ", empty));
    }
}
