using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Der Ablauf einer Pause im Overlay, ohne Fenster: höchstens ein Satz Overlays, Ende durch
/// Ablauf, Nutzer oder Schließen von außen, ein Aufbaufehler, der die Pause sauber beendet,
/// statt die App mit gesetztem Overlay-Zustand hängen zu lassen, und ein eigenes Bild, das bei
/// jedem Fehlschlag der Farbe weicht.
/// </summary>
public sealed class BreakOverlayPresenterTests
{
    private const string Color = "#F20F1115";
    private const string Picture = @"C:\Bilder\wald.jpg";

    // Deckkraft 0xF2 über Weiß, ganzzahlig: ((Kanal * 242) + (255 * 13)) / 255,
    // also 0x0F → 27, 0x11 → 29, 0x15 → 32.
    private static readonly (byte R, byte G, byte B) OpaqueColor = (27, 29, 32);

    private readonly FakeBreakOverlayUi _ui = new();
    private readonly FakeOverlayImageInspector _images = new();
    private readonly List<BreakEndReason> _ended = [];
    private readonly BreakOverlayPresenter _presenter;

    public BreakOverlayPresenterTests()
        => _presenter = new BreakOverlayPresenter(_ui, _images, NullLogger<BreakOverlayPresenter>.Instance);

    private void ShowFor(TimeSpan duration, bool allScreens = true)
        => _presenter.Show(BreakModel.ClassicPomodoro, duration, new OverlayBackdrop(Color, null, 0), allScreens, _ended.Add);

    private void ShowWithPicture(int dimPercent = 40)
        => _presenter.Show(BreakModel.ClassicPomodoro, TimeSpan.FromSeconds(30), new OverlayBackdrop(Color, Picture, dimPercent), true, _ended.Add);

    [Fact]
    public void Show_OpensTheWindowsWithAnOpaqueBackground()
    {
        ShowFor(TimeSpan.FromSeconds(30), allScreens: true);

        Assert.True(_presenter.IsOverlayOpen);
        Assert.Equal(1, _ui.OpenCount);
        Assert.True(_ui.AllScreens);
        Assert.Equal(OverlayLook.ForColor(OpaqueColor), _ui.Look);
        Assert.Null(_images.InspectedPath);
    }

    [Fact]
    public void Show_WhileOpen_OpensNoSecondSet()
    {
        ShowFor(TimeSpan.FromSeconds(30));

        ShowFor(TimeSpan.FromSeconds(30));

        Assert.Equal(1, _ui.OpenCount);
    }

    [Fact]
    public void Show_WithoutDuration_OpensNothing()
    {
        ShowFor(TimeSpan.Zero);

        Assert.False(_presenter.IsOverlayOpen);
        Assert.Equal(0, _ui.OpenCount);
    }

    [Fact]
    public void Countdown_TicksTheSharedStateAndRefreshesTheWindows()
    {
        ShowFor(TimeSpan.FromSeconds(30));

        _ui.Tick();

        Assert.Equal(29, _ui.ViewModel!.RemainingSeconds); // nach Show gesetzt
        Assert.Equal(1, _ui.RefreshCount);
    }

    [Fact]
    public void ElapsedBreak_ClosesTheWindowsAndReportsElapsed()
    {
        ShowFor(TimeSpan.FromSeconds(2));

        _ui.Tick();
        _ui.Tick();

        Assert.Equal([BreakEndReason.Elapsed], _ended);
        Assert.False(_presenter.IsOverlayOpen);
        Assert.Equal(1, _ui.CloseCount);
    }

    [Fact]
    public void EndedByUser_ClosesTheWindowsAndReportsIt()
    {
        ShowFor(TimeSpan.FromSeconds(30));

        _ui.ViewModel!.EndByUser(); // nach Show gesetzt

        Assert.Equal([BreakEndReason.EndedByUser], _ended);
        Assert.False(_presenter.IsOverlayOpen);
    }

    /// <remarks>Schließen von außen, etwa beim Beenden der App, meldet kein Pausenende.</remarks>
    [Fact]
    public void Close_ClosesTheWindowsWithoutReportingAnEnd()
    {
        ShowFor(TimeSpan.FromSeconds(30));

        _presenter.Close();

        Assert.False(_presenter.IsOverlayOpen);
        Assert.Equal(1, _ui.CloseCount);
        Assert.Empty(_ended);
    }

    [Fact]
    public void Close_WithoutOverlay_DoesNothing()
    {
        _presenter.Close();

        Assert.Equal(0, _ui.CloseCount);
    }

    [Fact]
    public void FailedWindowSetup_EndsTheBreakAsElapsed()
    {
        _ui.OpenFailure = new InvalidOperationException("Vollbild gescheitert");

        ShowFor(TimeSpan.FromSeconds(30));

        Assert.Equal([BreakEndReason.Elapsed], _ended);
        Assert.False(_presenter.IsOverlayOpen);
        Assert.Equal(1, _ui.CloseCount);
    }

    [Fact]
    public void Show_WithAReadablePicture_DrawsItOverTheColor()
    {
        _images.Result = new OverlayImageInfo(6000, 4000, (200, 150, 100));

        ShowWithPicture(dimPercent: 40);

        OverlayImageLayer image = Assert.IsType<OverlayImageLayer>(_ui.Look?.Image);
        Assert.Equal(Picture, _images.InspectedPath);
        Assert.Equal(OpaqueColor, _ui.Look!.Color); // mit Image gesetzt
        Assert.Equal(Picture, image.Path);
        Assert.Equal((6000, 4000), (image.PixelWidth, image.PixelHeight));
        Assert.Equal(0.4, image.DimOpacity, precision: 3);
    }

    /// <remarks>
    /// Die Schrift steht auf dem abgedunkelten Bild, nicht auf der Farbe darunter: 40 % Schwarz
    /// über (200, 150, 100) lässt (120, 90, 60).
    /// </remarks>
    [Fact]
    public void Show_WithAPicture_ChoosesTheInkAgainstTheDimmedPicture()
    {
        _images.Result = new OverlayImageInfo(10, 10, (200, 150, 100));

        ShowWithPicture(dimPercent: 40);

        Assert.Equal(((byte)120, (byte)90, (byte)60), _ui.Look!.TextSurface); // Fenster geöffnet
    }

    [Fact]
    public void Show_WithAnUnreadablePicture_ShowsTheColor()
    {
        _images.Result = null;

        ShowWithPicture();

        Assert.Equal(OverlayLook.ForColor(OpaqueColor), _ui.Look);
        Assert.True(_presenter.IsOverlayOpen);
    }

    [Fact]
    public void Show_WhenInspectingThePictureFails_ShowsTheColor()
    {
        _images.Failure = new InvalidOperationException("Decoder abgestürzt");

        ShowWithPicture();

        Assert.Equal(OverlayLook.ForColor(OpaqueColor), _ui.Look);
        Assert.Empty(_ended);
    }

    [Fact]
    public void Show_WhileThePictureIsRead_OpensTheWindowsOnlyAfterwards()
    {
        _images.HoldAnswers();

        ShowWithPicture();
        Assert.Equal(0, _ui.OpenCount);

        _images.Complete(new OverlayImageInfo(10, 10, (0, 0, 0)));
        Assert.Equal(1, _ui.OpenCount);
    }

    /// <remarks>
    /// Endet die Pause, während das Bild noch gelesen wird, darf danach kein Overlay mehr
    /// aufgehen: Es bliebe ohne Pause offen stehen.
    /// </remarks>
    [Fact]
    public void Close_WhileThePictureIsRead_OpensNoWindowsAfterwards()
    {
        _images.HoldAnswers();
        ShowWithPicture();

        _presenter.Close();
        _images.Complete(new OverlayImageInfo(10, 10, (0, 0, 0)));

        Assert.Equal(0, _ui.OpenCount);
        Assert.False(_presenter.IsOverlayOpen);
    }

    [Fact]
    public void Show_WithoutBackdrop_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => _presenter.Show(BreakModel.ClassicPomodoro, TimeSpan.FromSeconds(1), null!, true, _ended.Add));

    [Fact]
    public void Constructor_WithoutImageInspector_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new BreakOverlayPresenter(_ui, null!, NullLogger<BreakOverlayPresenter>.Instance));
}
