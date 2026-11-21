using LookAway.Core.Services;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Core.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Core.Tests;

/// <summary>
/// Deterministische Tests für den <see cref="TimerService"/>:
/// Zeit wird per <see cref="FakeClock.Advance"/> gesteuert und der
/// Phasenwechsel über <c>Tick()</c> ausgelöst. Der Hintergrund-Loop
/// läuft mit einem absurd hohen Tickintervall, damit er die
/// Tests nicht stört.
/// </summary>
public sealed class TimerServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly BreakInterval ClassicPomodoro = BreakInterval.Create(
        TimeSpan.FromMinutes(25),
        TimeSpan.FromMinutes(5));

    // Der Takt steht auf 24 Stunden: Phasenwechsel lösen die Tests selbst über Tick aus.
    private static TimerService CreateService(out FakeClock clock, out FakePowerModeWatcher power)
    {
        power = new FakePowerModeWatcher();
        return CreateService(power, out clock);
    }

    // Für Tests, die Standby und Aufwachen selbst auslösen; sie entsorgen die Attrappe selbst.
    private static TimerService CreateService(FakePowerModeWatcher power, out FakeClock clock)
    {
        clock = new FakeClock(Start);
        return new TimerService(clock, power, NullLogger<TimerService>.Instance, TimeSpan.FromHours(24));
    }

    [Fact]
    public void Initial_StateIsIdle()
    {
        using TimerService service = CreateService(out _, out _);
        Assert.Equal(TimerState.Idle, service.State);
        Assert.Null(service.CurrentInterval);
        Assert.Equal(TimeSpan.Zero, service.Remaining);
    }

    [Fact]
    public void Start_TransitionsToWorkingState()
    {
        using TimerService service = CreateService(out _, out _);
        service.Start(ClassicPomodoro);

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(ClassicPomodoro, service.CurrentInterval);
        Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);
    }

    [Fact]
    public void RestoreWorking_SetsRemainingWorkTimeAndState()
    {
        using TimerService service = CreateService(out _, out _);
        service.RestoreWorking(ClassicPomodoro, TimeSpan.FromMinutes(7));

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(ClassicPomodoro, service.CurrentInterval);
        Assert.Equal(TimeSpan.FromMinutes(7), service.Remaining);
    }

    [Fact]
    public void RestoreWorking_CapsRemainingTimeAtTheFullWorkDuration()
    {
        using TimerService service = CreateService(out _, out _);
        service.RestoreWorking(ClassicPomodoro, TimeSpan.FromMinutes(999));

        Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);
    }

    [Fact]
    public void Tick_BeforeWorkDurationElapsed_StaysWorking()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        service.Tick();

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);
    }

    [Fact]
    public async Task Tick_AfterWorkDurationElapsed_TransitionsToOnBreakAndEmitsEvent()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(25));
        service.Tick();

        Assert.Equal(TimerState.OnBreak, service.State);
        BreakDueEvent breakDue = await ReadEventAsync<BreakDueEvent>(service);
        Assert.Equal(ClassicPomodoro, breakDue.Interval);
    }

    [Fact]
    public async Task Tick_AfterBreakDurationElapsed_EmitsBreakCompletedAndWorkResumed()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(25));
        service.Tick();
        _ = await ReadEventAsync<BreakDueEvent>(service);

        clock.Advance(TimeSpan.FromMinutes(5));
        service.Tick();

        Assert.Equal(TimerState.Working, service.State);
        _ = await ReadEventAsync<BreakCompletedEvent>(service);
        _ = await ReadEventAsync<WorkResumedEvent>(service);
    }

    [Fact]
    public async Task Pause_FromWorking_StoresRemainingTime()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        service.Pause();

        Assert.Equal(TimerState.Paused, service.State);
        Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);

        TimerPausedEvent paused = await ReadEventAsync<TimerPausedEvent>(service);
        Assert.Equal(TimerState.Working, paused.PausedFromState);
        Assert.False(paused.PausedBySystem);
    }

    [Fact]
    public async Task Resume_AfterUserPause_RestoresWorkingStateWithRemainingTime()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        service.Pause();
        _ = await ReadEventAsync<TimerPausedEvent>(service);

        clock.Advance(TimeSpan.FromMinutes(7));
        service.ResumeAfterPause();

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);

        _ = await ReadEventAsync<WorkResumedEvent>(service);

        // Tick nach 14 Minuten — Phase noch nicht abgelaufen.
        clock.Advance(TimeSpan.FromMinutes(14));
        service.Tick();
        Assert.Equal(TimerState.Working, service.State);

        // Tick nach insgesamt 15 Minuten Restzeit — Phase läuft ab.
        clock.Advance(TimeSpan.FromMinutes(1));
        service.Tick();
        Assert.Equal(TimerState.OnBreak, service.State);
    }

    [Fact]
    public void Pause_FromIdle_HasNoEffect()
    {
        using TimerService service = CreateService(out _, out _);
        service.Pause();
        Assert.Equal(TimerState.Idle, service.State);
    }

    [Fact]
    public void ResumeAfterPause_WhenNotPaused_HasNoEffect()
    {
        using TimerService service = CreateService(out _, out _);
        service.Start(ClassicPomodoro);
        service.ResumeAfterPause();
        Assert.Equal(TimerState.Working, service.State);
    }

    [Fact]
    public async Task SystemSuspend_PausesTimerAndMarksAsSystem()
    {
        using FakePowerModeWatcher power = new();
        using TimerService service = CreateService(power, out FakeClock clock);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        power.RaiseSuspending();

        Assert.Equal(TimerState.Paused, service.State);
        Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);

        TimerPausedEvent paused = await ReadEventAsync<TimerPausedEvent>(service);
        Assert.True(paused.PausedBySystem);
    }

    /// <remarks>
    /// War der Rechner mindestens eine Pausenlänge im Standby, hat der Nutzer nicht auf den
    /// Bildschirm geschaut — die Augen haben bereits pausiert. Der Arbeits-Timer startet daher
    /// frisch (volle Arbeitszeit), statt mit der Restzeit weiterzulaufen.
    /// </remarks>
    [Fact]
    public async Task SystemResume_AfterLongSleep_RestartsWorkPhase()
    {
        using FakePowerModeWatcher power = new();
        using TimerService service = CreateService(power, out FakeClock clock);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        power.RaiseSuspending();
        _ = await ReadEventAsync<TimerPausedEvent>(service);

        // 30 min Sleep >= 5 min Pause -> Neustart der Arbeitsphase.
        clock.Advance(TimeSpan.FromMinutes(30));
        power.RaiseResuming();

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);

        _ = await ReadEventAsync<WorkResumedEvent>(service);

        // Erst nach vollen 25 weiteren Minuten ist die frische Phase abgelaufen.
        clock.Advance(TimeSpan.FromMinutes(24));
        service.Tick();
        Assert.Equal(TimerState.Working, service.State);

        clock.Advance(TimeSpan.FromMinutes(1));
        service.Tick();
        Assert.Equal(TimerState.OnBreak, service.State);
    }

    [Fact]
    public async Task SystemResume_AfterLongSleepDuringBreak_EndsBreakAndRestartsWork()
    {
        // Sleep während einer laufenden Pause: nach langem Standby ist die Pause
        // ohnehin vorbei. Es wird ein reguläres Pausenende signalisiert
        // (BreakCompleted -> Overlay/Helligkeit zurücksetzen) und frisch gearbeitet.
        using FakePowerModeWatcher power = new();
        using TimerService service = CreateService(power, out FakeClock clock);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(25));
        service.Tick(); // -> OnBreak
        _ = await ReadEventAsync<BreakDueEvent>(service);

        clock.Advance(TimeSpan.FromMinutes(1));
        power.RaiseSuspending();
        _ = await ReadEventAsync<TimerPausedEvent>(service);

        clock.Advance(TimeSpan.FromMinutes(30));
        power.RaiseResuming();

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);

        _ = await ReadEventAsync<BreakCompletedEvent>(service);
        _ = await ReadEventAsync<WorkResumedEvent>(service);
    }

    [Fact]
    public async Task SystemResume_AfterShortSleep_ContinuesWorking()
    {
        using FakePowerModeWatcher power = new();
        using TimerService service = CreateService(power, out FakeClock clock);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        power.RaiseSuspending();
        _ = await ReadEventAsync<TimerPausedEvent>(service);

        clock.Advance(TimeSpan.FromMinutes(2));
        power.RaiseResuming();

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);

        _ = await ReadEventAsync<WorkResumedEvent>(service);
    }

    [Fact]
    public void SystemResume_DoesNotResumeUserPause()
    {
        using FakePowerModeWatcher power = new();
        using TimerService service = CreateService(power, out FakeClock clock);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(5));
        service.Pause();

        // System sendet ein Resume-Event, obwohl die Pause vom Benutzer kam.
        power.RaiseResuming();

        Assert.Equal(TimerState.Paused, service.State);
    }

    [Fact]
    public async Task ResumeAfterAway_ShortAway_ResumesRemainingTime()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        service.Pause();
        _ = await ReadEventAsync<TimerPausedEvent>(service);

        // 2 min Abwesenheit < 5 min Pause -> Restzeit (15 min) läuft weiter.
        service.ResumeAfterAway(TimeSpan.FromMinutes(2));

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);
    }

    [Fact]
    public async Task ResumeAfterAway_LongAway_RestartsFullWorkPhase()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        service.Pause();
        _ = await ReadEventAsync<TimerPausedEvent>(service);

        // 5 min Abwesenheit >= 5 min Pause -> frische Arbeitsphase (25 min).
        service.ResumeAfterAway(TimeSpan.FromMinutes(5));

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);
    }

    [Fact]
    public void ResumeAfterAway_WhenPausedBySystem_IsNoOp()
    {
        using FakePowerModeWatcher power = new();
        using TimerService service = CreateService(power, out FakeClock clock);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));
        power.RaiseSuspending(); // System-Pause

        service.ResumeAfterAway(TimeSpan.FromMinutes(30));

        // System-Pausen werden über den Power-Resume-Pfad behandelt, nicht hier.
        Assert.Equal(TimerState.Paused, service.State);
    }

    [Fact]
    public void ResumeAfterAway_WhenNotPaused_IsNoOp()
    {
        using TimerService service = CreateService(out FakeClock clock, out _);
        service.Start(ClassicPomodoro);
        clock.Advance(TimeSpan.FromMinutes(10));

        service.ResumeAfterAway(TimeSpan.FromMinutes(30));

        Assert.Equal(TimerState.Working, service.State);
        Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);
    }

    [Fact]
    public async Task Stop_FromWorkingState_ResetsToIdleAndEmitsStopEvent()
    {
        using TimerService service = CreateService(out _, out _);
        service.Start(ClassicPomodoro);
        service.StopCycle();

        Assert.Equal(TimerState.Idle, service.State);
        Assert.Null(service.CurrentInterval);
        _ = await ReadEventAsync<TimerStoppedEvent>(service);
    }

    [Fact]
    public async Task Stop_WhenAlreadyIdle_DoesNotEmitEvent()
    {
        using TimerService service = CreateService(out _, out _);
        service.StopCycle();

        Assert.Equal(TimerState.Idle, service.State);

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(150));
        bool eventReceived = false;
        try
        {
            await foreach (TimerEvent _ in service.Events.WithCancellation(cts.Token))
            {
                eventReceived = true;
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // erwartet — kein Event in der Wartezeit
        }

        Assert.False(eventReceived);
    }

    [Fact]
    public void Constructor_RejectsNullClock()
    {
        using FakePowerModeWatcher power = new();
        _ = Assert.Throws<ArgumentNullException>(
            () => new TimerService(null!, power, NullLogger<TimerService>.Instance));
    }

    [Fact]
    public void Constructor_RejectsNullPowerWatcher()
    {
        FakeClock clock = new(Start);
        _ = Assert.Throws<ArgumentNullException>(
            () => new TimerService(clock, null!, NullLogger<TimerService>.Instance));
    }

    [Fact]
    public void Constructor_RejectsNullLogger()
    {
        FakeClock clock = new(Start);
        using FakePowerModeWatcher power = new();
        _ = Assert.Throws<ArgumentNullException>(
            () => new TimerService(clock, power, null!));
    }

    [Fact]
    public void OperationsAfterDispose_ThrowObjectDisposed()
    {
        TimerService service = CreateService(out _, out _);
        service.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => service.Start(ClassicPomodoro));
        _ = Assert.Throws<ObjectDisposedException>(service.Pause);
        _ = Assert.Throws<ObjectDisposedException>(service.ResumeAfterPause);
        _ = Assert.Throws<ObjectDisposedException>(service.StopCycle);
    }

    private static async Task<T> ReadEventAsync<T>(TimerService service) where T : TimerEvent
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
        await foreach (TimerEvent evt in service.Events.WithCancellation(cts.Token))
        {
            if (evt is T match)
            {
                return match;
            }
        }

        throw new InvalidOperationException($"Erwartetes Event {typeof(T).Name} nicht erhalten.");
    }
}
