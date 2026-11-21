using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests;

/// <summary>
/// Fortsetzen des Countdowns nach einem Neustart: nur in derselben Windows-Sitzung, nur zum selben
/// Modell, nur mit Restzeit, und nur einmal.
/// </summary>
public sealed class TimerResumptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    // Seit dem Systemstart sind zwei Stunden vergangen; die Sitzungsmarke ist damit acht Uhr.
    private const long MillisecondsSinceBoot = 2 * 60 * 60 * 1000;

    private readonly FakeTimerStateStore _store = new();
    private readonly TimerResumption _resumption;

    public TimerResumptionTests()
        => _resumption = new TimerResumption(_store, new FakeClock(Now), static () => MillisecondsSinceBoot);

    private static DateTimeOffset ThisSession => SessionMarker.Compute(Now, MillisecondsSinceBoot);

    [Fact]
    public void Remember_StoresTheRestWithTheSessionMarker()
    {
        _resumption.Remember(BreakModel.Ultradian, TimeSpan.FromMinutes(42));

        Assert.Equal(new TimerSnapshot(BreakModel.Ultradian, TimeSpan.FromMinutes(42), ThisSession), _store.Stored);
    }

    [Fact]
    public void Take_InTheSameSession_ReturnsTheRestOnce()
    {
        _store.Stored = new TimerSnapshot(BreakModel.Ultradian, TimeSpan.FromMinutes(42), ThisSession);

        Assert.Equal(TimeSpan.FromMinutes(42), _resumption.Take(BreakModel.Ultradian));
        Assert.Null(_resumption.Take(BreakModel.Ultradian));
    }

    [Fact]
    public void Take_WithoutSnapshot_StartsFresh()
        => Assert.Null(_resumption.Take(BreakModel.Ultradian));

    /// <remarks>Nach einem Windows-Neustart beginnt die Arbeitsphase neu; die Aufnahme wird trotzdem verworfen.</remarks>
    [Fact]
    public void Take_FromAnEarlierSession_StartsFreshAndDiscardsIt()
    {
        _store.Stored = new TimerSnapshot(BreakModel.Ultradian, TimeSpan.FromMinutes(42), ThisSession.AddHours(-5));

        Assert.Null(_resumption.Take(BreakModel.Ultradian));
        Assert.Equal(1, _store.ClearCount);
    }

    [Fact]
    public void Take_ForAnotherModel_StartsFresh()
    {
        _store.Stored = new TimerSnapshot(BreakModel.Ultradian, TimeSpan.FromMinutes(42), ThisSession);

        Assert.Null(_resumption.Take(BreakModel.ClassicPomodoro));
    }

    [Fact]
    public void Take_WithoutRemainingTime_StartsFresh()
    {
        _store.Stored = new TimerSnapshot(BreakModel.Ultradian, TimeSpan.Zero, ThisSession);

        Assert.Null(_resumption.Take(BreakModel.Ultradian));
    }
}
