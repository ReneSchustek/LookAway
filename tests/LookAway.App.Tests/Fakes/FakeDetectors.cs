using LookAway.Core.Domain;
using LookAway.Core.Interfaces;

namespace LookAway.App.Tests.Fakes;

/// <summary>Inaktivität mit frei setzbarer Dauer.</summary>
internal sealed class FakeIdleDetector : IIdleDetector
{
    public TimeSpan IdleTime { get; set; }

    public TimeSpan GetIdleTime() => IdleTime;
}

/// <summary>Vollbild-Erkennung mit frei setzbarem Ergebnis.</summary>
internal sealed class FakeFullscreenDetector : IFullscreenDetector
{
    public bool IsActive { get; set; }

    public bool IsFullscreenApplicationActive() => IsActive;
}

/// <summary>
/// Nimmt die Meldungen der Hintergrund-Schleifen entgegen und lässt auf die erste Meldung
/// jeder Art warten.
/// </summary>
internal sealed class FakeBreakSignalSink : IBreakSignalSink
{
    private readonly TaskCompletionSource<TimerEvent> _firstEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _firstDndUpdate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _firstReminder = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TimerEvent> FirstEvent => _firstEvent.Task;

    public Task<bool> FirstDndUpdate => _firstDndUpdate.Task;

    public Task FirstReminder => _firstReminder.Task;

    public void HandleTimerEvent(TimerEvent timerEvent) => _firstEvent.TrySetResult(timerEvent);

    public void RequestReminder() => _firstReminder.TrySetResult();

    public void UpdateDndIndicator(bool fullscreenDndActive) => _firstDndUpdate.TrySetResult(fullscreenDndActive);
}
