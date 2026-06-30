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
        FakeBreakHistoryRepository History,
        FakeClock Clock);

    private static void Run(Action<Harness> body, Settings? settings = null, bool applyInitialSchedule = true)
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

        if (applyInitialSchedule)
        {
            coordinator.ApplySchedule(settings ?? new Settings());
        }

        body(new Harness(coordinator, timer, reminder, overlay, tray, history, clock));
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

    [Fact]
    public void ApplySchedule_MitUnveraendertemIntervall_SetztDenTimerNichtZurueck() => Run(h =>
    {
        TimeSpan voll = h.Timer.Remaining;
        h.Clock.Advance(TimeSpan.FromMinutes(10));
        TimeSpan nachAblauf = h.Timer.Remaining;

        // Erneutes Anwenden derselben Einstellungen (z. B. nach Speichern einer
        // unabhängigen Option) darf den laufenden Countdown nicht zurücksetzen.
        h.Coordinator.ApplySchedule(new Settings());

        Assert.True(h.Timer.Remaining <= nachAblauf + TimeSpan.FromSeconds(1));
        Assert.True(h.Timer.Remaining < voll);
    });

    [Fact]
    public void ApplySchedule_BeiModellwechsel_StartetDenTimerNeu() => Run(h =>
    {
        h.Clock.Advance(TimeSpan.FromMinutes(10));

        // Anderes Modell -> anderes Intervall -> bewusster Neustart auf volle Arbeitsdauer.
        h.Coordinator.ApplySchedule(new Settings { BreakModel = BreakModel.Ultradian });

        BreakInterval ultra = BreakModelRegistry.GetEffective(BreakModel.Ultradian, null);
        Assert.True(h.Timer.Remaining > ultra.WorkDuration - TimeSpan.FromSeconds(2));
        Assert.Equal(BreakModel.Ultradian, h.Tray.ActiveModel);
    });

    [Fact]
    public void ApplySchedule_MitResume_SetztDenCountdownFortStattZurueckzusetzen() => Run(
        h =>
        {
            // Frischer Coordinator (Timer noch Idle): Wiederherstellung nach Neustart
            // in derselben Sitzung setzt mit der Restzeit fort statt auf voller Dauer.
            h.Coordinator.ApplySchedule(new Settings(), TimeSpan.FromMinutes(8));

            Assert.Equal(TimerState.Working, h.Timer.State);
            Assert.Equal(TimeSpan.FromMinutes(8), h.Timer.Remaining);
        },
        applyInitialSchedule: false);
}
