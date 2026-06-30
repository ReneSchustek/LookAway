using LookAway.Application.Services;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Tests.Unit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Unit.Application.Services;

/// <summary>
/// Tests fuer den <see cref="IdleDetectionService"/> mit
/// <see cref="FakeIdleDetector"/> und einem realen, ueber <see cref="FakeClock"/>
/// gesteuerten <see cref="TimerService"/>. Der Timer wird je Test ueber
/// <see cref="WithService"/> deterministisch freigegeben.
/// </summary>
public sealed class IdleDetectionServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 27, 9, 0, 0, TimeSpan.Zero);

    private static readonly BreakInterval ClassicPomodoro = BreakInterval.Create(
        TimeSpan.FromMinutes(25),
        TimeSpan.FromMinutes(5));

    [Fact]
    public void Evaluate_IdleBelowThreshold_DoesNotPause() => WithService((service, idle, timer) =>
    {
        timer.Start(ClassicPomodoro);
        idle.IdleTime = TimeSpan.FromMinutes(1);

        service.Evaluate();

        Assert.Equal(TimerState.Working, timer.State);
        Assert.False(service.IsPausedByIdle);
    });

    [Fact]
    public void Evaluate_IdleAboveThreshold_PausesTimer() => WithService((service, idle, timer) =>
    {
        timer.Start(ClassicPomodoro);
        idle.IdleTime = TimeSpan.FromMinutes(6);

        service.Evaluate();

        Assert.Equal(TimerState.Paused, timer.State);
        Assert.True(service.IsPausedByIdle);
    });

    [Fact]
    public void Evaluate_ActivityAfterIdlePause_ResumesTimer() => WithService((service, idle, timer) =>
    {
        timer.Start(ClassicPomodoro);
        idle.IdleTime = TimeSpan.FromMinutes(6);
        service.Evaluate();

        idle.IdleTime = TimeSpan.Zero;
        service.Evaluate();

        Assert.Equal(TimerState.Working, timer.State);
        Assert.False(service.IsPausedByIdle);
    });

    [Fact]
    public void Evaluate_ActivityAfterLongIdle_RestartsWorkPhase() => WithService((service, idle, timer) =>
    {
        timer.Start(ClassicPomodoro);
        // 6 min Inaktivitaet >= 5 min Pause: der Nutzer hat nicht auf den Schirm
        // geschaut, daher startet der Arbeits-Timer beim Zurueckkehren frisch.
        idle.IdleTime = TimeSpan.FromMinutes(6);
        service.Evaluate();

        idle.IdleTime = TimeSpan.Zero;
        service.Evaluate();

        Assert.Equal(TimerState.Working, timer.State);
        Assert.Equal(TimeSpan.FromMinutes(25), timer.Remaining);
        Assert.False(service.IsPausedByIdle);
    });

    [Fact]
    public void Evaluate_TimerIdle_DoesNotPause() => WithService((service, idle, timer) =>
    {
        idle.IdleTime = TimeSpan.FromMinutes(10);

        service.Evaluate();

        Assert.Equal(TimerState.Idle, timer.State);
        Assert.False(service.IsPausedByIdle);
    });

    [Fact]
    public void Evaluate_Disabled_DoesNotPause() => WithService((service, idle, timer) =>
    {
        timer.Start(ClassicPomodoro);
        service.IsEnabled = false;
        idle.IdleTime = TimeSpan.FromMinutes(10);

        service.Evaluate();

        Assert.Equal(TimerState.Working, timer.State);
    });

    [Fact]
    public void Evaluate_DisabledWhilePausedByIdle_Resumes() => WithService((service, idle, timer) =>
    {
        timer.Start(ClassicPomodoro);
        idle.IdleTime = TimeSpan.FromMinutes(6);
        service.Evaluate();
        Assert.True(service.IsPausedByIdle);

        service.IsEnabled = false;
        service.Evaluate();

        Assert.Equal(TimerState.Working, timer.State);
        Assert.False(service.IsPausedByIdle);
    });

    [Fact]
    public void Evaluate_UserPause_NotAutoResumed() => WithService((service, idle, timer) =>
    {
        timer.Start(ClassicPomodoro);
        timer.Pause(); // Benutzer-Pause, nicht durch Idle ausgeloest
        idle.IdleTime = TimeSpan.Zero;

        service.Evaluate();

        Assert.Equal(TimerState.Paused, timer.State);
        Assert.False(service.IsPausedByIdle);
    });

    private static void WithService(Action<IdleDetectionService, FakeIdleDetector, TimerService> body)
    {
        FakeIdleDetector idle = new();
        FakeClock clock = new(Start);
        using FakePowerModeWatcher power = new();
        using TimerService timer = new(clock, power, NullLogger<TimerService>.Instance, TimeSpan.FromHours(24));
        IdleDetectionService service = new(idle, timer, NullLogger<IdleDetectionService>.Instance);
        body(service, idle, timer);
    }
}
