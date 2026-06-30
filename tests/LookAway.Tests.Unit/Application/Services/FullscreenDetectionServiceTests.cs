using LookAway.Application.Services;
using LookAway.Tests.Unit.Fakes;

namespace LookAway.Tests.Unit.Application.Services;

/// <summary>
/// Tests für den <see cref="FullscreenDetectionService"/> mit
/// <see cref="FakeFullscreenDetector"/>: DND-Zustand und Nachhol-Logik der
/// verpassten Erinnerung.
/// </summary>
public sealed class FullscreenDetectionServiceTests
{
    [Fact]
    public void TryShowReminder_NoFullscreen_AllowsReminder()
    {
        (FullscreenDetectionService service, FakeFullscreenDetector detector) = Create();
        detector.IsActive = false;
        _ = service.Evaluate();

        Assert.True(service.TryShowReminder());
        Assert.False(service.IsDndActive);
    }

    [Fact]
    public void Evaluate_FullscreenActive_SetsDnd()
    {
        (FullscreenDetectionService service, FakeFullscreenDetector detector) = Create();
        detector.IsActive = true;

        _ = service.Evaluate();

        Assert.True(service.IsDndActive);
    }

    [Fact]
    public void TryShowReminder_DuringDnd_SuppressesAndRemembers()
    {
        (FullscreenDetectionService service, FakeFullscreenDetector detector) = Create();
        detector.IsActive = true;
        _ = service.Evaluate();

        bool shown = service.TryShowReminder();

        Assert.False(shown);
        Assert.True(service.HasPendingReminder);
    }

    [Fact]
    public void Evaluate_FullscreenEnds_SurfacesMissedReminderOnce()
    {
        (FullscreenDetectionService service, FakeFullscreenDetector detector) = Create();
        detector.IsActive = true;
        _ = service.Evaluate();
        _ = service.TryShowReminder(); // unterdrückt + gemerkt

        detector.IsActive = false;
        bool surfaceNow = service.Evaluate();

        Assert.True(surfaceNow);
        Assert.False(service.HasPendingReminder);
        Assert.False(service.Evaluate()); // nur einmal
    }

    [Fact]
    public void Evaluate_FullscreenEndsWithoutMissedReminder_ReturnsFalse()
    {
        (FullscreenDetectionService service, FakeFullscreenDetector detector) = Create();
        detector.IsActive = true;
        _ = service.Evaluate();

        detector.IsActive = false;
        Assert.False(service.Evaluate());
    }

    [Fact]
    public void Disabled_IgnoresFullscreen_AllowsReminder()
    {
        (FullscreenDetectionService service, FakeFullscreenDetector detector) = Create();
        detector.IsActive = true;
        service.IsEnabled = false;

        _ = service.Evaluate();

        Assert.False(service.IsDndActive);
        Assert.True(service.TryShowReminder());
    }

    private static (FullscreenDetectionService Service, FakeFullscreenDetector Detector) Create()
    {
        FakeFullscreenDetector detector = new();
        FullscreenDetectionService service = new(detector);
        return (service, detector);
    }
}
