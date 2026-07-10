using LookAway.Core.Interfaces;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Deterministischer <see cref="IClock"/> für Tests. Die Zeit wird per
/// <see cref="Advance"/> oder <see cref="SetUtcNow"/> gesteuert; der
/// Monotonic-Counter läuft synchron zur Wallclock.
/// </summary>
internal sealed class FakeClock : IClock
{
    private readonly Lock _lock = new();
    private DateTimeOffset _utcNow;
    private TimeSpan _monotonic;

    public FakeClock(DateTimeOffset initial)
    {
        _utcNow = initial;
        _monotonic = TimeSpan.Zero;
    }

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_lock)
            {
                return _utcNow;
            }
        }
    }

    public TimeSpan MonotonicElapsed
    {
        get
        {
            lock (_lock)
            {
                return _monotonic;
            }
        }
    }

    public void Advance(TimeSpan delta)
    {
        lock (_lock)
        {
            _utcNow = _utcNow + delta;
            _monotonic = _monotonic + delta;
        }
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        lock (_lock)
        {
            _utcNow = value;
        }
    }
}
