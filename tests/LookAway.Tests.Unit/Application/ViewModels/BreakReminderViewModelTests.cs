using LookAway.Application.ViewModels;
using LookAway.Core.Enums;

namespace LookAway.Tests.Unit.Application.ViewModels;

/// <summary>
/// Tests der UI-freien Aktionslogik der Pause-Erinnerung
/// (<see cref="BreakReminderViewModel"/>): Aktionen, Timeout-Default und
/// Einmaligkeit.
/// </summary>
public sealed class BreakReminderViewModelTests
{
    private const string HintKey = "BreakHint.Pomodoro";

    [Fact]
    public void Constructor_RejectsEmptyHintKey()
    {
        _ = Assert.Throws<ArgumentException>(() => new BreakReminderViewModel(" "));
    }

    [Fact]
    public void StartBreak_SetsResultAndRaisesCompletedOnce()
    {
        BreakReminderViewModel vm = new(HintKey);
        int raised = 0;
        ReminderResult? captured = null;
        vm.Completed += (_, e) =>
        {
            raised++;
            captured = e.ChosenAction;
        };

        vm.StartBreak();

        Assert.True(vm.IsCompleted);
        Assert.Equal(ReminderResult.StartBreak, vm.Result);
        Assert.Equal(ReminderResult.StartBreak, captured);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Snooze_SetsSnoozeResult()
    {
        BreakReminderViewModel vm = new(HintKey);

        vm.Snooze();

        Assert.Equal(ReminderResult.Snooze, vm.Result);
    }

    [Fact]
    public void Skip_SetsSkipResult()
    {
        BreakReminderViewModel vm = new(HintKey);

        vm.Skip();

        Assert.Equal(ReminderResult.Skip, vm.Result);
    }

    [Fact]
    public void TimeoutElapsed_WithoutAction_DefaultsToStartBreak()
    {
        BreakReminderViewModel vm = new(HintKey);

        vm.TimeoutElapsed();

        Assert.Equal(ReminderResult.StartBreak, vm.Result);
    }

    [Fact]
    public void TimeoutElapsed_AfterUserAction_DoesNotOverride()
    {
        BreakReminderViewModel vm = new(HintKey);
        vm.Snooze();

        vm.TimeoutElapsed();

        Assert.Equal(ReminderResult.Snooze, vm.Result);
    }

    [Fact]
    public void SecondAction_IsIgnored()
    {
        BreakReminderViewModel vm = new(HintKey);
        int raised = 0;
        vm.Completed += (_, _) => raised++;

        vm.Skip();
        vm.StartBreak();

        Assert.Equal(ReminderResult.Skip, vm.Result);
        Assert.Equal(1, raised);
    }
}
