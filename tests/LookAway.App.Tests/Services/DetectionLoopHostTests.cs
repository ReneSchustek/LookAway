using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Die Hintergrund-Schleifen: Die Erkennung meldet „Nicht stören“ und holt eine ausgefallene
/// Erinnerung nach, der Ereignisstrom des Timers erreicht den Pausen-Ablauf, und nach dem Beenden
/// läuft keine Schleife weiter. Der Abfragetakt ist für die Tests auf wenige Millisekunden gesetzt.
/// </summary>
public sealed class DetectionLoopHostTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly FakeTimerService _timer = new();
    private readonly FakeFullscreenDetector _fullscreenDetector = new();
    private readonly FakeBreakSignalSink _sink = new();
    private readonly IdleDetectionService _idle;
    private readonly FullscreenDetectionService _fullscreen;
    private readonly DetectionLoopHost _host;

    public DetectionLoopHostTests()
    {
        _idle = new IdleDetectionService(new FakeIdleDetector(), _timer, NullLogger<IdleDetectionService>.Instance);
        _fullscreen = new FullscreenDetectionService(_fullscreenDetector);
        _host = new DetectionLoopHost(_idle, _fullscreen, _timer, _sink, pollInterval: TimeSpan.FromMilliseconds(10));
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void ApplySettings_ConfiguresBothDetections()
    {
        _host.ApplySettings(new Settings { PauseOnIdle = false, IdleThresholdMinutes = 12, SuppressOnFullscreen = false });

        Assert.False(_idle.IsEnabled);
        Assert.Equal(TimeSpan.FromMinutes(12), _idle.Threshold);
        Assert.False(_fullscreen.IsEnabled);
    }

    [Fact]
    public async Task DetectionLoop_ReportsTheFullscreenState()
    {
        _fullscreenDetector.IsActive = true;

        _host.Start();

        Assert.True(await _sink.FirstDndUpdate.WaitAsync(Wait, TestContext.Current.CancellationToken));
    }

    /// <remarks>
    /// Fiel eine Erinnerung in die Vollbild-Zeit, kommt sie, sobald das Vollbild endet — nicht
    /// erst zur nächsten regulären Pause.
    /// </remarks>
    [Fact]
    public async Task DetectionLoop_CatchesUpOnAReminderMissedDuringFullscreen()
    {
        _fullscreenDetector.IsActive = true;
        _ = _fullscreen.Evaluate();
        Assert.False(_fullscreen.TryShowReminder());
        _fullscreenDetector.IsActive = false;

        _host.Start();

        await _sink.FirstReminder.WaitAsync(Wait, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EventLoop_PassesTimerEventsOn()
    {
        _host.Start();

        await _timer.EventChannel.Writer.WriteAsync(new BreakCompletedEvent(), TestContext.Current.CancellationToken);

        _ = Assert.IsType<BreakCompletedEvent>(await _sink.FirstEvent.WaitAsync(Wait, TestContext.Current.CancellationToken));
    }

    /// <remarks>Nach dem Beenden erreicht kein Ereignis mehr den Pausen-Ablauf.</remarks>
    [Fact]
    public async Task Dispose_StopsBothLoops()
    {
        _host.Start();

        _host.Dispose();
        _host.Dispose();
        await _timer.EventChannel.Writer.WriteAsync(new BreakCompletedEvent(), TestContext.Current.CancellationToken);

        Task finished = await Task.WhenAny(_sink.FirstEvent, Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken));
        Assert.NotSame(_sink.FirstEvent, finished);
    }
}
