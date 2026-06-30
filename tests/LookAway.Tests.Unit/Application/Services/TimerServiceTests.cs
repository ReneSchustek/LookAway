using LookAway.Application.Services;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Tests.Unit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Unit.Application.Services;

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

    private static (TimerService Service, FakeClock Clock, FakePowerModeWatcher Power) CreateService()
    {
        FakeClock clock = new(Start);
        FakePowerModeWatcher power = new();
        TimerService service = new(clock, power, NullLogger<TimerService>.Instance, TimeSpan.FromHours(24));
        return (service, clock, power);
    }

    [Fact]
    public void Initial_StateIsIdle()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            Assert.Equal(TimerState.Idle, service.State);
            Assert.Null(service.CurrentInterval);
            Assert.Equal(TimeSpan.Zero, service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Start_TransitionsToWorkingState()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);

            Assert.Equal(TimerState.Working, service.State);
            Assert.Equal(ClassicPomodoro, service.CurrentInterval);
            Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void RestoreWorking_SetztVerbleibendeArbeitszeitUndZustand()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            service.RestoreWorking(ClassicPomodoro, TimeSpan.FromMinutes(7));

            Assert.Equal(TimerState.Working, service.State);
            Assert.Equal(ClassicPomodoro, service.CurrentInterval);
            Assert.Equal(TimeSpan.FromMinutes(7), service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void RestoreWorking_BegrenztRestzeitAufVolleArbeitsdauer()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            service.RestoreWorking(ClassicPomodoro, TimeSpan.FromMinutes(999));

            Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Tick_BeforeWorkDurationElapsed_StaysWorking()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));
            service.Tick();

            Assert.Equal(TimerState.Working, service.State);
            Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Tick_AfterWorkDurationElapsed_TransitionsToOnBreakAndEmitsEvent()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(25));
            service.Tick();

            Assert.Equal(TimerState.OnBreak, service.State);
            BreakDueEvent breakDue = await ReadEventAsync<BreakDueEvent>(service);
            Assert.Equal(ClassicPomodoro, breakDue.Interval);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Tick_AfterBreakDurationElapsed_EmitsBreakCompletedAndWorkResumed()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
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
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Pause_FromWorking_StoresRemainingTime()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));
            service.Pause();

            Assert.Equal(TimerState.Paused, service.State);
            Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);

            TimerPausedEvent paused = await ReadEventAsync<TimerPausedEvent>(service);
            Assert.Equal(TimerState.Working, paused.PausedFromState);
            Assert.False(paused.PausedBySystem);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Resume_AfterUserPause_RestoresWorkingStateWithRemainingTime()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));
            service.Pause();
            _ = await ReadEventAsync<TimerPausedEvent>(service);

            clock.Advance(TimeSpan.FromMinutes(7));
            service.Resume();

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
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Pause_FromIdle_HasNoEffect()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            service.Pause();
            Assert.Equal(TimerState.Idle, service.State);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Resume_WhenNotPaused_HasNoEffect()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            service.Resume();
            Assert.Equal(TimerState.Working, service.State);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SystemSuspend_PausesTimerAndMarksAsSystem()
    {
        (TimerService service, FakeClock clock, FakePowerModeWatcher power) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));
            power.RaiseSuspending();

            Assert.Equal(TimerState.Paused, service.State);
            Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);

            TimerPausedEvent paused = await ReadEventAsync<TimerPausedEvent>(service);
            Assert.True(paused.PausedBySystem);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SystemResume_AfterLongSleep_RestartsWorkPhase()
    {
        // Semantik: War der Rechner mindestens eine Pausenlänge im Standby, hat
        // der Nutzer nicht auf den Bildschirm geschaut — die Augen haben bereits
        // pausiert. Der Arbeits-Timer startet daher frisch (volle Arbeitszeit),
        // statt mit der Restzeit weiterzulaufen.
        (TimerService service, FakeClock clock, FakePowerModeWatcher power) = CreateService();
        try
        {
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
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SystemResume_AfterLongSleepDuringBreak_EndsBreakAndRestartsWork()
    {
        // Sleep während einer laufenden Pause: nach langem Standby ist die Pause
        // ohnehin vorbei. Es wird ein reguläres Pausenende signalisiert
        // (BreakCompleted -> Overlay/Helligkeit zurücksetzen) und frisch gearbeitet.
        (TimerService service, FakeClock clock, FakePowerModeWatcher power) = CreateService();
        try
        {
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
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task SystemResume_AfterShortSleep_ContinuesWorking()
    {
        (TimerService service, FakeClock clock, FakePowerModeWatcher power) = CreateService();
        try
        {
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
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void SystemResume_DoesNotResumeUserPause()
    {
        (TimerService service, FakeClock clock, FakePowerModeWatcher power) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(5));
            service.Pause();

            // System sendet ein Resume-Event, obwohl die Pause vom Benutzer kam.
            power.RaiseResuming();

            Assert.Equal(TimerState.Paused, service.State);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task ResumeAfterAway_ShortAway_ResumesRemainingTime()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));
            service.Pause();
            _ = await ReadEventAsync<TimerPausedEvent>(service);

            // 2 min Abwesenheit < 5 min Pause -> Restzeit (15 min) läuft weiter.
            service.ResumeAfterAway(TimeSpan.FromMinutes(2));

            Assert.Equal(TimerState.Working, service.State);
            Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task ResumeAfterAway_LongAway_RestartsFullWorkPhase()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));
            service.Pause();
            _ = await ReadEventAsync<TimerPausedEvent>(service);

            // 5 min Abwesenheit >= 5 min Pause -> frische Arbeitsphase (25 min).
            service.ResumeAfterAway(TimeSpan.FromMinutes(5));

            Assert.Equal(TimerState.Working, service.State);
            Assert.Equal(TimeSpan.FromMinutes(25), service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void ResumeAfterAway_WhenPausedBySystem_IsNoOp()
    {
        (TimerService service, FakeClock clock, FakePowerModeWatcher power) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));
            power.RaiseSuspending(); // System-Pause

            service.ResumeAfterAway(TimeSpan.FromMinutes(30));

            // System-Pausen werden über den Power-Resume-Pfad behandelt, nicht hier.
            Assert.Equal(TimerState.Paused, service.State);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void ResumeAfterAway_WhenNotPaused_IsNoOp()
    {
        (TimerService service, FakeClock clock, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            clock.Advance(TimeSpan.FromMinutes(10));

            service.ResumeAfterAway(TimeSpan.FromMinutes(30));

            Assert.Equal(TimerState.Working, service.State);
            Assert.Equal(TimeSpan.FromMinutes(15), service.Remaining);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Stop_FromWorkingState_ResetsToIdleAndEmitsStopEvent()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            service.Start(ClassicPomodoro);
            service.Stop();

            Assert.Equal(TimerState.Idle, service.State);
            Assert.Null(service.CurrentInterval);
            _ = await ReadEventAsync<TimerStoppedEvent>(service);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Stop_WhenAlreadyIdle_DoesNotEmitEvent()
    {
        (TimerService service, _, _) = CreateService();
        try
        {
            service.Stop();

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
        finally
        {
            service.Dispose();
        }
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
        (TimerService service, _, _) = CreateService();
        service.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => service.Start(ClassicPomodoro));
        _ = Assert.Throws<ObjectDisposedException>(service.Pause);
        _ = Assert.Throws<ObjectDisposedException>(service.Resume);
        _ = Assert.Throws<ObjectDisposedException>(service.Stop);
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
