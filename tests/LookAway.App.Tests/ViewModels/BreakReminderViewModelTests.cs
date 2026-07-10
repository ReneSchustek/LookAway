using LookAway.App.ViewModels;
using LookAway.Core.Enums;

namespace LookAway.App.Tests;

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
    public void Tick_ZaehltHerunterUndStartetPauseBeiNull()
    {
        BreakReminderViewModel vm = new(HintKey, autoStartSeconds: 3);

        vm.Tick();
        Assert.Equal(2, vm.RemainingSeconds);
        Assert.False(vm.IsCompleted);

        vm.Tick();
        Assert.Equal(1, vm.RemainingSeconds);
        Assert.False(vm.IsCompleted);

        vm.Tick();
        Assert.Equal(0, vm.RemainingSeconds);
        Assert.Equal(ReminderResult.StartBreak, vm.Result);
    }

    [Fact]
    public void Tick_OhneAutoStart_WirktNicht()
    {
        BreakReminderViewModel vm = new(HintKey);

        Assert.False(vm.AutoStartsAutomatically);

        for (int i = 0; i < 5; i++)
        {
            vm.Tick();
        }

        Assert.False(vm.IsCompleted);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void Tick_NachBenutzeraktion_UeberschreibtNicht()
    {
        BreakReminderViewModel vm = new(HintKey, autoStartSeconds: 1);
        vm.Snooze();

        vm.Tick();

        Assert.Equal(ReminderResult.Snooze, vm.Result);
    }

    [Fact]
    public void Constructor_MitAutoStart_SetztAnfangswerte()
    {
        BreakReminderViewModel vm = new(HintKey, autoStartSeconds: 15);

        Assert.True(vm.AutoStartsAutomatically);
        Assert.Equal(15, vm.RemainingSeconds);
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
