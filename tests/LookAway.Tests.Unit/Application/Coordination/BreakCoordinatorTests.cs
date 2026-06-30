using LookAway.Application.Coordination;
using LookAway.Application.Services;
using LookAway.Application.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Tests.Unit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Unit.Application.Coordination;

/// <summary>
/// Tests für den <see cref="BreakCoordinator"/>: der Pausen-Ablauf
/// (Timer-Ereignisse, Erinnerung, Overlay, Historie, Tray) wird über Fakes
/// deterministisch geprüft.
/// </summary>
public sealed class BreakCoordinatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 30, 9, 0, 0, TimeSpan.Zero);

    private sealed record Harness(
        BreakCoordinator Coordinator,
        TimerService Timer,
        FakeReminderPresenter Reminder,
        FakeBreakOverlayPresenter Overlay,
        FakeTrayController Tray,
        FakeBreakHistoryRepository History);

    private static void Run(Action<Harness> body, Settings? settings = null)
    {
        FakeClock clock = new(Start);
        using FakePowerModeWatcher power = new();
        using TimerService timer = new(clock, power, NullLogger<TimerService>.Instance, TimeSpan.FromHours(24));
        PauseActionService pauseActions = new(new FakeScreenDimmer(), new FakeMediaController());
        FakeReminderPresenter reminder = new();
        FakeBreakOverlayPresenter overlay = new();
        FakeTrayController tray = new();
        FakeBreakHistoryRepository history = new();
        FullscreenDetectionService fullscreen = new(new FakeFullscreenDetector());

        BreakCoordinator coordinator = new(
            timer,
            reminder,
            overlay,
            pauseActions,
            new FakeSoundService(),
            clock,
            history,
            tray,
            fullscreen,
            NullLogger<BreakCoordinator>.Instance);

        coordinator.ApplySchedule(settings ?? new Settings());
        body(new Harness(coordinator, timer, reminder, overlay, tray, history));
    }

    private static IReadOnlyList<BreakSession> Sessions(Harness h)
        => h.History.LoadAllAsync().GetAwaiter().GetResult();

    [Fact]
    public void ApplySchedule_StartsTimerAndSetsTrayModel()
        => Run(
            h =>
            {
                Assert.Equal(TimerState.Working, h.Timer.State);
                Assert.Equal(BreakModel.Ultradian, h.Tray.ActiveModel);
            },
            new Settings { BreakModel = BreakModel.Ultradian });

    [Fact]
    public void BreakDue_ShowsReminder_WhenNotSuppressed() => Run(h =>
    {
        h.Coordinator.HandleTimerEvent(new BreakDueEvent(BreakInterval.Create(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5))));

        Assert.Equal(1, h.Reminder.ShowCount);
    });

    [Fact]
    public void StartBreak_ShowsOverlayAndRecordsTakenSession() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);

        Assert.Equal(1, h.Overlay.ShowCount);
        Assert.True(h.Overlay.IsOverlayOpen);
        Assert.Equal(BreakOutcome.Taken, Assert.Single(Sessions(h)).Outcome);
    });

    [Fact]
    public void Snooze_RestartsTimerWithSnoozeWindow_AndRecordsSnoozed() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.Snooze);

        Assert.Equal(TimerState.Working, h.Timer.State);
        Assert.Equal(TimeSpan.FromMinutes(BreakReminderViewModel.SnoozeMinutes), h.Timer.Remaining);
        Assert.Equal(BreakOutcome.Snoozed, Assert.Single(Sessions(h)).Outcome);
    });

    [Fact]
    public void Skip_RestartsWorkInterval_AndRecordsSkipped() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.Skip);

        Assert.Equal(TimerState.Working, h.Timer.State);
        Assert.Equal(TimeSpan.FromMinutes(25), h.Timer.Remaining);
        Assert.Equal(BreakOutcome.Skipped, Assert.Single(Sessions(h)).Outcome);
    });

    [Fact]
    public void OverlayEnded_RestartsWorkInterval() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);

        h.Overlay.EndWith(BreakEndReason.EndedByUser);

        Assert.Equal(TimerState.Working, h.Timer.State);
        Assert.Equal(TimeSpan.FromMinutes(25), h.Timer.Remaining);
    });

    [Fact]
    public void BreakCompleted_WithOpenOverlay_LeavesOverlayInCharge() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);

        h.Coordinator.HandleTimerEvent(new BreakCompletedEvent());

        Assert.True(h.Overlay.IsOverlayOpen);
    });

    [Fact]
    public void ManualDnd_SuppressesReminder() => Run(h =>
    {
        h.Coordinator.ToggleManualDnd();
        h.Coordinator.RequestReminder();

        Assert.Equal(0, h.Reminder.ShowCount);
        Assert.True(h.Tray.DndActive);
        Assert.True(h.Coordinator.IsManualDndActive);
    });

    [Fact]
    public void UpdateDndIndicator_CombinesFullscreenAndManual() => Run(h =>
    {
        h.Coordinator.UpdateDndIndicator(fullscreenDndActive: true);
        Assert.True(h.Tray.DndActive);

        h.Coordinator.UpdateDndIndicator(fullscreenDndActive: false);
        Assert.False(h.Tray.DndActive);
    });

    [Fact]
    public void ReminderAlreadyOpen_DoesNotShowSecond() => Run(h =>
    {
        h.Reminder.IsReminderOpen = true;
        h.Coordinator.RequestReminder();

        Assert.Equal(0, h.Reminder.ShowCount);
    });
}
