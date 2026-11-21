using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Die Erinnerung ohne Fenster: höchstens eine offen, die Wahl des Benutzers kommt beim Pausen-
/// Ablauf an, und ein Aufbaufehler blockiert die nächsten Erinnerungen nicht.
/// </summary>
public sealed class ReminderPresenterTests
{
    private readonly List<BreakReminderViewModel> _viewModels = [];
    private readonly List<FakeAppWindow> _windows = [];
    private readonly List<ReminderResult> _results = [];
    private Exception? _windowFailure;

    private ReminderPresenter CreatePresenter()
        => new(
            new ImmediateUiDispatcher(),
            viewModel =>
            {
                if (_windowFailure is not null)
                {
                    throw _windowFailure;
                }

                _viewModels.Add(viewModel);
                FakeAppWindow window = new();
                _windows.Add(window);
                return window;
            },
            NullLogger<ReminderPresenter>.Instance);

    [Fact]
    public void Show_OpensAndActivatesOneWindow()
    {
        ReminderPresenter presenter = CreatePresenter();

        presenter.Show(BreakModel.ClassicPomodoro, null, _results.Add);

        Assert.True(presenter.IsReminderOpen);
        Assert.Equal(1, Assert.Single(_windows).ActivateCount);
    }

    [Fact]
    public void Show_WhileOpen_OpensNoSecondWindow()
    {
        ReminderPresenter presenter = CreatePresenter();
        presenter.Show(BreakModel.ClassicPomodoro, null, _results.Add);

        presenter.Show(BreakModel.ClassicPomodoro, null, _results.Add);

        _ = Assert.Single(_windows);
    }

    /// <remarks>Angefangene Sekunden zählen voll: Bei 2,1 s steht der Countdown auf 3.</remarks>
    [Fact]
    public void AutoStart_RoundsUpToWholeSeconds()
    {
        ReminderPresenter presenter = CreatePresenter();

        presenter.Show(BreakModel.ClassicPomodoro, TimeSpan.FromSeconds(2.1), _results.Add);

        Assert.Equal(3, Assert.Single(_viewModels).RemainingSeconds);
    }

    [Fact]
    public void UserChoice_ReachesTheCallerAndFreesTheSlot()
    {
        ReminderPresenter presenter = CreatePresenter();
        presenter.Show(BreakModel.ClassicPomodoro, null, _results.Add);

        _viewModels[0].Snooze();

        Assert.Equal([ReminderResult.Snooze], _results);
        Assert.False(presenter.IsReminderOpen);
    }

    /// <remarks>Ein gescheitertes Fenster gilt als übersprungen; die nächste Erinnerung kommt trotzdem.</remarks>
    [Fact]
    public void FailedWindow_CountsAsSkippedAndKeepsLaterRemindersPossible()
    {
        ReminderPresenter presenter = CreatePresenter();
        _windowFailure = new InvalidOperationException("Fenster gescheitert");

        presenter.Show(BreakModel.ClassicPomodoro, null, _results.Add);
        _windowFailure = null;
        presenter.Show(BreakModel.ClassicPomodoro, null, _results.Add);

        Assert.Equal([ReminderResult.Skip], _results);
        _ = Assert.Single(_windows);
    }
}
