using LookAway.Core.Services;

namespace LookAway.Core.Tests;

/// <summary>
/// Der Hintergrund-Takt des Timers: Er ruft seine Aktion auf, endet nach einem Abbruch und meldet
/// einen unerwarteten Fehler, statt still stehenzubleiben.
/// </summary>
public sealed class TimerTickLoopTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Running_CallsTheTick()
    {
        TaskCompletionSource ticked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using TimerTickLoop loop = new(TimeSpan.FromMilliseconds(5), ticked.SetResult, _ => { });

        loop.EnsureRunning();

        await ticked.Task.WaitAsync(Wait, TestContext.Current.CancellationToken);
    }

    /// <remarks>Ein Fehler im Takt endet im Protokoll, nicht als stehengebliebene Uhr ohne Spur.</remarks>
    [Fact]
    public async Task FailingTick_IsReported()
    {
        TaskCompletionSource<Exception> crashed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using TimerTickLoop loop = new(
            TimeSpan.FromMilliseconds(5),
            () => throw new InvalidOperationException("Takt gescheitert"),
            exception => crashed.TrySetResult(exception));

        loop.EnsureRunning();

        _ = Assert.IsType<InvalidOperationException>(await crashed.Task.WaitAsync(Wait, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CancelAndDispose_WithoutStart_AreHarmless()
    {
        using TimerTickLoop loop = new(TimeSpan.FromMilliseconds(5), () => { }, _ => { });
        loop.Cancel();
        loop.Dispose();

        // Das using entsorgt ein zweites Mal; auch ein dritter Aufruf schadet nicht.
        Exception? thrown = Record.Exception(loop.Dispose);

        Assert.Null(thrown);
    }
}
