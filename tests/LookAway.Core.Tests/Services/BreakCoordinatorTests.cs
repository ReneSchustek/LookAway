using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Core.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für den <see cref="BreakCoordinator"/>: der Pausen-Ablauf
/// (Timer-Ereignisse, Erinnerung, Overlay, Historie, Tray) wird über Fakes
/// deterministisch geprüft.
/// </summary>
public sealed class BreakCoordinatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 30, 9, 0, 0, TimeSpan.Zero);

    // Arbeitsdauer des Vorgabemodells; neue Einstellungen starten mit ihm.
    private static readonly TimeSpan DefaultWorkDuration =
        BreakModelRegistry.GetDefault(Settings.DefaultBreakModel).WorkDuration;

    // Der Coordinator samt aller Attrappen, damit jeder Test an jede Seite herankommt.
    private sealed record Harness(
        BreakCoordinator Coordinator,
        TimerService Timer,
        FakeReminderPresenter Reminder,
        FakeBreakOverlayPresenter Overlay,
        FakeTrayController Tray,
        FakeBreakHistoryRepository History,
        FakeClock Clock,
        FakeSoundService Sound);

    private static void Run(Action<Harness> body, Settings? settings = null, bool applyInitialSchedule = true)
    {
        FakeClock clock = new(Start);
        using FakePowerModeWatcher power = new();
        using TimerService timer = new(clock, power, NullLogger<TimerService>.Instance, TimeSpan.FromHours(24));
        using PauseActionService pauseActions = new(new FakeScreenDimmer(), new FakeMediaController());
        FakeReminderPresenter reminder = new();
        FakeBreakOverlayPresenter overlay = new();
        FakeTrayController tray = new();
        FakeBreakHistoryRepository history = new();
        FullscreenDetectionService fullscreen = new(new FakeFullscreenDetector());
        FakeSoundService sound = new();

        BreakCoordinator coordinator = new(
            timer,
            new BreakPresenters(reminder, overlay, tray),
            pauseActions,
            sound,
            clock,
            new BreakHistoryRecorder(history, NullLogger<BreakHistoryRecorder>.Instance),
            fullscreen);

        if (applyInitialSchedule)
        {
            coordinator.ApplySchedule(settings ?? new Settings());
        }

        body(new Harness(coordinator, timer, reminder, overlay, tray, history, clock, sound));
    }

    private static IReadOnlyList<BreakSession> Sessions(Harness h)
        => h.History.Sessions;

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
    public void StartBreak_ShowsTheOverlayAsConfigured()
        => Run(
            h =>
            {
                h.Coordinator.RequestReminder();
                h.Reminder.CompleteWith(ReminderResult.StartBreak);

                Assert.Equal(new OverlayBackdrop("#123456", @"C:\Bilder\wald.jpg", 60), h.Overlay.LastBackdrop);
                Assert.False(h.Overlay.LastDarkenAllScreens);
            },
            new Settings
            {
                BreakOverlayColor = "#123456",
                BreakOverlayBackground = OverlayBackground.Image,
                BreakOverlayImagePath = @"C:\Bilder\wald.jpg",
                BreakOverlayImageDimPercent = 60,
                DarkenAllScreens = false,
            });

    [Fact]
    public void StartBreak_ShowsOverlay_AndRecordsOnlyWhenTheBreakEnds() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);

        Assert.Equal(1, h.Overlay.ShowCount);
        Assert.True(h.Overlay.IsOverlayOpen);
        Assert.Empty(Sessions(h));

        h.Overlay.EndWith(BreakEndReason.Elapsed);

        Assert.Equal(BreakOutcome.Taken, Assert.Single(Sessions(h)).Outcome);
    });

    /// <remarks>
    /// Die Statistik zählt gemachte Pausenzeit. Wer die Pause per Esc nach einer Minute
    /// beendet, darf nicht mit der vollen geplanten Dauer in der Historie stehen.
    /// </remarks>
    [Fact]
    public void BreakEndedEarly_RecordsTheActualDuration() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);

        h.Clock.Advance(TimeSpan.FromMinutes(1));
        h.Overlay.EndWith(BreakEndReason.EndedByUser);

        BreakSession session = Assert.Single(Sessions(h));
        Assert.Equal(BreakOutcome.Taken, session.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(1), session.Duration);
    });

    [Fact]
    public void BreakEndedByHotkey_RecordsTheActualDuration() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);

        h.Clock.Advance(TimeSpan.FromMinutes(3));
        h.Coordinator.SkipOrSnooze();

        Assert.Equal(TimeSpan.FromMinutes(3), Assert.Single(Sessions(h)).Duration);
    });

    [Fact]
    public void BreakDue_PassesTheAutoStartDelayFromTheSettings() => Run(
        h =>
        {
            h.Coordinator.RequestReminder();

            Assert.Equal(TimeSpan.FromSeconds(45), h.Reminder.LastAutoStartAfter);
        },
        settings: new Settings { AutoStartBreakEnabled = true, AutoStartBreakSeconds = 45 });

    [Fact]
    public void BreakDue_WithoutAutoStart_PassesNoDelay() => Run(
        h =>
        {
            h.Coordinator.RequestReminder();

            Assert.Null(h.Reminder.LastAutoStartAfter);
        },
        settings: new Settings { AutoStartBreakEnabled = false });

    [Fact]
    public void Snooze_RestartsTimerWithSnoozeWindow_AndRecordsSnoozed() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.Snooze);

        Assert.Equal(TimerState.Working, h.Timer.State);
        Assert.Equal(BreakDefaults.SnoozeWorkDuration, h.Timer.Remaining);
        Assert.Equal(BreakOutcome.Snoozed, Assert.Single(Sessions(h)).Outcome);
    });

    [Fact]
    public void Skip_RestartsWorkInterval_AndRecordsSkipped() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.Skip);

        Assert.Equal(TimerState.Working, h.Timer.State);
        Assert.Equal(DefaultWorkDuration, h.Timer.Remaining);
        Assert.Equal(BreakOutcome.Skipped, Assert.Single(Sessions(h)).Outcome);
    });

    [Fact]
    public void OverlayEnded_RestartsWorkInterval() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);

        h.Overlay.EndWith(BreakEndReason.EndedByUser);

        Assert.Equal(TimerState.Working, h.Timer.State);
        Assert.Equal(DefaultWorkDuration, h.Timer.Remaining);
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
    public void ApplySchedule_WithAnUnchangedInterval_DoesNotResetTheTimer() => Run(h =>
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
    public void ApplySchedule_OnModelChange_RestartsTheTimer() => Run(h =>
    {
        h.Clock.Advance(TimeSpan.FromMinutes(10));

        // Anderes Modell -> anderes Intervall -> bewusster Neustart auf volle Arbeitsdauer.
        h.Coordinator.ApplySchedule(new Settings { BreakModel = BreakModel.Ultradian });

        BreakInterval ultra = BreakModelRegistry.GetEffective(BreakModel.Ultradian, null);
        Assert.True(h.Timer.Remaining > ultra.WorkDuration - TimeSpan.FromSeconds(2));
        Assert.Equal(BreakModel.Ultradian, h.Tray.ActiveModel);
    });

    [Fact]
    public void ApplySchedule_WithResume_ContinuesTheCountdownInsteadOfResetting() => Run(
        h =>
        {
            // Frischer Coordinator (Timer noch Idle): Wiederherstellung nach Neustart
            // in derselben Sitzung setzt mit der Restzeit fort statt auf voller Dauer.
            h.Coordinator.ApplySchedule(new Settings(), TimeSpan.FromMinutes(8));

            Assert.Equal(TimerState.Working, h.Timer.State);
            Assert.Equal(TimeSpan.FromMinutes(8), h.Timer.Remaining);
        },
        applyInitialSchedule: false);

    /// <remarks>
    /// Das aktive Modell wird beim Beenden in die Momentaufnahme geschrieben und beim
    /// nächsten Start wieder aufgenommen. Läge hier etwas anderes als in den
    /// Einstellungen, setzte das Programm nach einem Neustart mit dem falschen Modell fort.
    /// </remarks>
    [Fact]
    public void ActiveModel_FollowsTheSchedule() => Run(h =>
    {
        Assert.Equal(Settings.DefaultBreakModel, h.Coordinator.ActiveModel);

        h.Coordinator.ApplySchedule(new Settings { BreakModel = BreakModel.Ultradian });

        Assert.Equal(BreakModel.Ultradian, h.Coordinator.ActiveModel);
    });

    /// <remarks>
    /// Beides zusammen bildet die Momentaufnahme beim Beenden: ob gearbeitet wurde und
    /// wie viel davon noch offen war. Wird die Restzeit falsch gemeldet, beginnt das
    /// Programm nach dem nächsten Start mitten in einer Phase, die es nicht mehr gibt.
    /// </remarks>
    [Fact]
    public void IsWorkingAndWorkRemaining_MirrorTheTimer() => Run(h =>
    {
        Assert.True(h.Coordinator.IsWorking);
        TimeSpan atStart = h.Coordinator.WorkRemaining;

        h.Clock.Advance(TimeSpan.FromMinutes(10));
        h.Timer.Tick();

        Assert.True(h.Coordinator.WorkRemaining < atStart);
    });

    [Fact]
    public void IsWorking_IsFalseBeforeAnySchedule() => Run(
        h => Assert.False(h.Coordinator.IsWorking),
        applyInitialSchedule: false);

    /// <remarks>
    /// Gegenstück zu <see cref="BreakCompleted_WithOpenOverlay_LeavesOverlayInCharge"/>:
    /// Wurde die Erinnerung übergangen, gibt es kein Overlay, das die Pause beendet —
    /// dann muss der Ablauf hier aufräumen, sonst bliebe der Bildschirm gedimmt und die
    /// Wiedergabe angehalten.
    /// </remarks>
    [Fact]
    public void BreakCompleted_WithoutOverlay_CleansUpItself() => Run(h =>
    {
        h.Coordinator.HandleTimerEvent(new BreakCompletedEvent());

        Assert.False(h.Overlay.IsOverlayOpen);
    });

    /// <summary>Der Hotkey „Überspringen" beginnt die Arbeitsphase von vorn.</summary>
    [Fact]
    public void SkipOrSnooze_RestartsTheWorkInterval() => Run(h =>
    {
        h.Clock.Advance(TimeSpan.FromMinutes(10));
        h.Timer.Tick();
        TimeSpan afterTick = h.Timer.Remaining;

        h.Coordinator.SkipOrSnooze();

        Assert.True(h.Timer.Remaining > afterTick);
    });

    [Fact]
    public void SkipOrSnooze_WithoutASchedule_DoesNothing() => Run(
        h =>
        {
            h.Coordinator.SkipOrSnooze();

            Assert.Equal(TimerState.Idle, h.Timer.State);
        },
        applyInitialSchedule: false);

    /// <remarks>
    /// Notausstieg aus der laufenden Pause: Das Overlay deckt jeden Monitor ab und liegt
    /// über allen Fenstern — ohne diesen Weg bliebe ESC der einzige Ausweg. Der Hotkey
    /// muss deshalb dasselbe leisten wie das reguläre Ende: Overlay zu, Arbeitsphase neu.
    /// </remarks>
    [Fact]
    public void SkipOrSnooze_WithOpenOverlay_ClosesOverlayAndRestartsWork() => Run(h =>
    {
        h.Coordinator.RequestReminder();
        h.Reminder.CompleteWith(ReminderResult.StartBreak);
        Assert.True(h.Overlay.IsOverlayOpen);

        h.Coordinator.SkipOrSnooze();

        Assert.False(h.Overlay.IsOverlayOpen);
        Assert.Equal(TimerState.Working, h.Timer.State);
        Assert.Equal(DefaultWorkDuration, h.Timer.Remaining);
    });

    /// <remarks>
    /// Der Ton ist das Einzige, was die Erinnerung ankündigt, wenn das Fenster hinter
    /// anderen liegt — Lautstärke und Art kommen aus den Einstellungen.
    /// </remarks>
    [Fact]
    public void Reminder_PlaysTheConfiguredSound() => Run(
        h =>
        {
            h.Coordinator.RequestReminder();

            Assert.Equal(1, h.Sound.PlayCallCount);
            Assert.Equal(SoundType.Bell, h.Sound.LastSound);
            Assert.Equal(40, h.Sound.LastVolume);
        },
        new Settings { SoundEnabled = true, ReminderSound = SoundType.Bell, SoundVolumePercent = 40 });

    [Fact]
    public void Reminder_StaysSilentWhenSoundIsOff() => Run(
        h =>
        {
            h.Coordinator.RequestReminder();

            Assert.Equal(0, h.Sound.PlayCallCount);
        },
        new Settings { SoundEnabled = false });
}
