using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;

namespace LookAway.App.Tests;

/// <summary>
/// Das Einstellungsfenster ohne Fenster: höchstens eines, gespeicherte Einstellungen, Hotkey-
/// Aufnahme und geänderte Aufgaben erreichen die App, und nach dem Schließen hängt nichts mehr
/// am alten ViewModel.
/// </summary>
public sealed class SettingsPresenterTests
{
    private readonly ImmediateUiDispatcher _dispatcher = new();
    private readonly List<SettingsViewModel> _viewModels = [];
    private readonly List<FakeAppWindow> _windows = [];
    private readonly List<Settings> _applied = [];
    private readonly List<bool> _capture = [];
    private int _tasksChanged;

    private SettingsPresenter CreatePresenter()
        => new(
            _dispatcher,
            () =>
            {
                SettingsViewModel viewModel = TestSettingsViewModels.Create(new InMemorySettingsRepository(new Settings()));
                _viewModels.Add(viewModel);
                return viewModel;
            },
            _ =>
            {
                FakeAppWindow window = new();
                _windows.Add(window);
                return window;
            },
            _applied.Add,
            _capture.Add,
            () => _tasksChanged++);

    [Fact]
    public void Show_OpensTheWindowOnTheUiThread()
    {
        SettingsPresenter presenter = CreatePresenter();

        presenter.Show();

        Assert.Equal(1, _dispatcher.PostCount);
        Assert.Equal(1, Assert.Single(_windows).ActivateCount);
    }

    [Fact]
    public async Task SecondOpen_BringsTheOpenWindowToTheFront()
    {
        SettingsPresenter presenter = CreatePresenter();
        await presenter.OpenWindowAsync();

        await presenter.OpenWindowAsync();

        FakeAppWindow window = Assert.Single(_windows);
        Assert.Equal(2, window.ActivateCount);
    }

    [Fact]
    public async Task SavedSettings_ReachTheApp()
    {
        SettingsPresenter presenter = CreatePresenter();
        await presenter.OpenWindowAsync();

        await _viewModels[0].ApplyCommand.ExecuteAsync(null);

        _ = Assert.Single(_applied);
    }

    [Fact]
    public async Task HotkeyCapture_IsReported()
    {
        SettingsPresenter presenter = CreatePresenter();
        await presenter.OpenWindowAsync();

        _viewModels[0].BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        Assert.Equal([true], _capture);
    }

    [Fact]
    public async Task ChangedTasks_AreReported()
    {
        SettingsPresenter presenter = CreatePresenter();
        await presenter.OpenWindowAsync();
        WorkTaskListViewModel tasks = _viewModels[0].Tasks;

        tasks.NewTaskText = "Angebot schreiben";
        await tasks.AddCommand.ExecuteAsync(null);

        Assert.Equal(1, _tasksChanged);
    }

    /// <remarks>
    /// Nach dem Schließen ist die Hotkey-Aufnahme in jedem Fall beendet, und ein neues Öffnen legt
    /// ein frisches Fenster samt ViewModel an. Das alte ViewModel meldet nichts mehr.
    /// </remarks>
    [Fact]
    public async Task Closing_EndsTheCaptureAndDetachesTheOldViewModel()
    {
        SettingsPresenter presenter = CreatePresenter();
        await presenter.OpenWindowAsync();
        SettingsViewModel first = _viewModels[0];

        _windows[0].Close();
        await first.ApplyCommand.ExecuteAsync(null);
        await presenter.OpenWindowAsync();

        Assert.Equal([false], _capture);
        Assert.Empty(_applied);
        Assert.Equal(2, _windows.Count);
        Assert.Equal(2, _viewModels.Count);
    }
}
