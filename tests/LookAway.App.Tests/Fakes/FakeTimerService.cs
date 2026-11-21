using System.Threading.Channels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Timer mit frei setzbarem Zustand; zählt, wie oft pausiert und fortgesetzt wurde.
/// </summary>
internal sealed class FakeTimerService : ITimerService
{
    public TimerState State { get; set; } = TimerState.Idle;

    public BreakInterval? CurrentInterval => null;

    public TimeSpan Remaining { get; set; }

    /// <summary>Was hier hineingeschrieben wird, liefert <see cref="Events"/> aus.</summary>
    public Channel<TimerEvent> EventChannel { get; } = Channel.CreateUnbounded<TimerEvent>();

    public IAsyncEnumerable<TimerEvent> Events => EventChannel.Reader.ReadAllAsync();

    public int PauseCount { get; private set; }

    public int ResumeCount { get; private set; }

    public void Start(BreakInterval interval)
    {
    }

    public void RestoreWorking(BreakInterval interval, TimeSpan workRemaining)
    {
    }

    public void StopCycle()
    {
    }

    public void Pause() => PauseCount++;

    public void ResumeAfterPause() => ResumeCount++;

    public void ResumeAfterAway(TimeSpan awayDuration)
    {
    }
}
