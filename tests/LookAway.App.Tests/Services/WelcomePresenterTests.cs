using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Der Wizard beim ersten Start ohne Fenster: Er meldet erst nach dem Schließen zurück, und zwar
/// ob er abgeschlossen oder vorher weggeklickt wurde.
/// </summary>
public sealed class WelcomePresenterTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly List<FakeAppWindow> _windows = [];
    private readonly List<Settings> _completed = [];
    private WelcomeViewModel? _viewModel;

    public void Dispose() => _viewModel?.Dispose();

    private WelcomePresenter CreatePresenter()
        => new(
            new ImmediateUiDispatcher(),
            () =>
            {
                InMemorySettingsRepository repository = new();
                _viewModel = new WelcomeViewModel(
                    repository,
                    new AutoStartCoordinator(new FakeAutoStartService(), repository, NullLogger<AutoStartCoordinator>.Instance),
                    new FakeLocalizationService(),
                    NullLogger<WelcomeViewModel>.Instance,
                    Language.German);
                return _viewModel;
            },
            _ =>
            {
                FakeAppWindow window = new();
                _windows.Add(window);
                return window;
            },
            _completed.Add);

    [Fact]
    public async Task ClosedWithoutFinishing_ReportsNotCompleted()
    {
        Task<bool> shown = CreatePresenter().ShowAsync();
        Assert.False(shown.IsCompleted);

        _windows[0].Close();

        Assert.False(await shown.WaitAsync(Wait, TestContext.Current.CancellationToken));
        Assert.Empty(_completed);
    }

    [Fact]
    public async Task Finished_ReportsCompletedWithTheChosenSettings()
    {
        Task<bool> shown = CreatePresenter().ShowAsync();
        _viewModel!.NextCommand.Execute(null); // die Fabrik hat das ViewModel beim Anzeigen angelegt
        _viewModel.NextCommand.Execute(null);
        await _viewModel.FinishCommand.ExecuteAsync(null);

        _windows[0].Close();

        Assert.True(await shown.WaitAsync(Wait, TestContext.Current.CancellationToken));
        _ = Assert.Single(_completed);
        Assert.Equal(1, _windows[0].ActivateCount);
    }
}
