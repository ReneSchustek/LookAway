using System.Reflection;
using LookAway.App.Services;

namespace LookAway.App.Tests;

/// <summary>
/// Das Startbild steht mindestens anderthalb Sekunden und schließt sich genau einmal.
/// Uhr und Warten sind vorgegeben; kein Test wartet wirklich.
/// </summary>
public sealed class SplashPresenterTests
{
    // Zählt Zeigen und Schließen, statt ein Fenster zu öffnen.
    private sealed class RecordingView : ISplashView
    {
        public int Shown { get; private set; }

        public int Closed { get; private set; }

        public Task ShowAsync()
        {
            Shown++;
            return Task.CompletedTask;
        }

        public void Close() => Closed++;
    }

    private TimeSpan _now = TimeSpan.FromMinutes(5);
    private readonly List<TimeSpan> _waits = [];

    private SplashPresenter Create(RecordingView view)
        => new(() => view, () => _now, delay =>
        {
            _waits.Add(delay);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task FastStart_WaitsForTheRestOfTheMinimumDisplayTime()
    {
        RecordingView view = new();
        SplashPresenter presenter = Create(view);

        await presenter.ShowAsync();
        _now += TimeSpan.FromMilliseconds(400);
        await presenter.CloseAsync();

        Assert.Equal([TimeSpan.FromMilliseconds(1100)], _waits);
        Assert.Equal(1, view.Closed);
    }

    [Fact]
    public async Task SlowStart_ClosesWithoutWaiting()
    {
        RecordingView view = new();
        SplashPresenter presenter = Create(view);

        await presenter.ShowAsync();
        _now += TimeSpan.FromSeconds(3);
        await presenter.CloseAsync();

        Assert.Empty(_waits);
        Assert.Equal(1, view.Closed);
    }

    /// <remarks>
    /// Beim ersten Start schließt der Wizard das Bild, das Ende des Starts versucht es noch
    /// einmal. Doppeltes Schließen eines WinUI-Fensters wirft; der zweite Aufruf muss leer bleiben.
    /// </remarks>
    [Fact]
    public async Task ShowAndClose_TwiceEach_ActOnce()
    {
        RecordingView view = new();
        SplashPresenter presenter = Create(view);

        await presenter.ShowAsync();
        await presenter.ShowAsync();
        _now += TimeSpan.FromSeconds(2);
        await presenter.CloseAsync();
        await presenter.CloseAsync();

        Assert.Equal(1, view.Shown);
        Assert.Equal(1, view.Closed);
    }

    [Fact]
    public async Task CloseWithoutShow_DoesNothing()
    {
        RecordingView view = new();

        await Create(view).CloseAsync();

        Assert.Equal(0, view.Closed);
        Assert.Empty(_waits);
    }

    [Theory]
    [InlineData(0, 1500)]
    [InlineData(1499, 1)]
    [InlineData(1500, 0)]
    [InlineData(9000, 0)]
    public void RemainingDisplayTime_NeverNegative(int shownMs, int expectedMs)
        => Assert.Equal(
            TimeSpan.FromMilliseconds(expectedMs),
            SplashPresenter.RemainingDisplayTime(TimeSpan.FromMilliseconds(shownMs)));

    /// <remarks>
    /// Das Startbild lädt sein Logo aus der Assembly. Fiele die Einbettung beim Umbau der
    /// Projektdatei weg, stünde ein leeres Bild da, ohne dass irgendetwas fehlschlägt.
    /// </remarks>
    [Fact]
    public void TheLogoIsEmbeddedUnderTheNameTheSplashWindowLoads()
    {
        Assembly app = typeof(SplashPresenter).Assembly;

        using Stream? logo = app.GetManifestResourceStream("LookAway.App.Assets.LookAwayLogo.png");

        Assert.NotNull(logo);
        Assert.True(logo.Length > 1000);
    }
}
