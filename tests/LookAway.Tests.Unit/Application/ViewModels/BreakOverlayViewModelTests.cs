using LookAway.Application.ViewModels;
using LookAway.Core.Enums;

namespace LookAway.Tests.Unit.Application.ViewModels;

/// <summary>
/// Tests der UI-freien Countdown- und Aktionslogik des Pausen-Overlays
/// (<see cref="BreakOverlayViewModel"/>): Initialisierung, Anzeige, Ablauf,
/// vorzeitiges Ende und Einmaligkeit.
/// </summary>
public sealed class BreakOverlayViewModelTests
{
    private const string HintKey = "BreakHint.Pomodoro";

    [Fact]
    public void Constructor_RejectsEmptyHintKey()
    {
        _ = Assert.Throws<ArgumentException>(() => new BreakOverlayViewModel(" ", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDuration()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BreakOverlayViewModel(HintKey, TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_RoundsRemainingSecondsUp()
    {
        BreakOverlayViewModel vm = new(HintKey, TimeSpan.FromMilliseconds(4500));

        Assert.Equal(5, vm.RemainingSeconds);
    }

    [Theory]
    [InlineData(305, "5:05")]
    [InlineData(60, "1:00")]
    [InlineData(9, "0:09")]
    public void RemainingDisplay_FormatsAsMinutesAndSeconds(int totalSeconds, string expected)
    {
        BreakOverlayViewModel vm = new(HintKey, TimeSpan.FromSeconds(totalSeconds));

        Assert.Equal(expected, vm.RemainingDisplay);
    }

    [Fact]
    public void Tick_DecrementsRemaining()
    {
        BreakOverlayViewModel vm = new(HintKey, TimeSpan.FromSeconds(10));

        vm.Tick();

        Assert.Equal(9, vm.RemainingSeconds);
        Assert.False(vm.IsEnded);
    }

    [Fact]
    public void Tick_AtZero_EndsWithElapsedOnce()
    {
        BreakOverlayViewModel vm = new(HintKey, TimeSpan.FromSeconds(2));
        int raised = 0;
        BreakEndReason? reason = null;
        vm.Ended += (_, e) =>
        {
            raised++;
            reason = e.Reason;
        };

        vm.Tick();
        vm.Tick();

        Assert.True(vm.IsEnded);
        Assert.Equal(0, vm.RemainingSeconds);
        Assert.Equal(BreakEndReason.Elapsed, reason);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Tick_AfterEnded_DoesNotRaiseAgain()
    {
        BreakOverlayViewModel vm = new(HintKey, TimeSpan.FromSeconds(1));
        int raised = 0;
        vm.Ended += (_, _) => raised++;

        vm.Tick();
        vm.Tick();

        Assert.Equal(1, raised);
        Assert.Equal(0, vm.RemainingSeconds);
    }

    [Fact]
    public void EndByUser_EndsWithUserReason()
    {
        BreakOverlayViewModel vm = new(HintKey, TimeSpan.FromMinutes(5));
        BreakEndReason? reason = null;
        vm.Ended += (_, e) => reason = e.Reason;

        vm.EndByUser();

        Assert.True(vm.IsEnded);
        Assert.Equal(BreakEndReason.EndedByUser, reason);
    }

    [Fact]
    public void EndByUser_ThenTick_KeepsUserReasonAndRaisesOnce()
    {
        BreakOverlayViewModel vm = new(HintKey, TimeSpan.FromSeconds(3));
        int raised = 0;
        BreakEndReason? reason = null;
        vm.Ended += (_, e) =>
        {
            raised++;
            reason = e.Reason;
        };

        vm.EndByUser();
        vm.Tick();

        Assert.Equal(BreakEndReason.EndedByUser, reason);
        Assert.Equal(1, raised);
    }
}
