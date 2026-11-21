using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests;

/// <summary>
/// Das Aussehen des Overlays in den Einstellungen: Farbe oder Bild, die Bildwahl mit ihrer
/// Formatprüfung, die Abdunklung und das Speichern.
/// </summary>
public sealed class SettingsViewModelOverlayTests : IDisposable
{
    private const string Picture = @"C:\Bilder\wald.jpg";

    private readonly FakeLocalizationService _texts = new();
    private readonly InMemorySettingsRepository _repository;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelOverlayTests()
    {
        _repository = new InMemorySettingsRepository(new Settings
        {
            BreakOverlayBackground = OverlayBackground.Image,
            BreakOverlayImagePath = Picture,
            BreakOverlayImageDimPercent = 65,
        });
        _viewModel = TestSettingsViewModels.Create(
            _repository,
            new FakeUpdateChecker(UpdateInfo.NoUpdate(new Version(1, 0, 0))),
            new FakeUpdateInstaller());
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task Load_ShowsPictureAndDimming()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(_viewModel.IsOverlayImageSelected);
        Assert.Equal(Picture, _viewModel.BreakOverlayImagePath);
        Assert.Equal("wald.jpg", _viewModel.OverlayImageName);
        Assert.Equal(65, _viewModel.BreakOverlayImageDimPercent);
    }

    [Fact]
    public async Task Choices_AreColorAndPictureInTheCurrentLanguage()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([OverlayBackground.Color, OverlayBackground.Image], _viewModel.OverlayBackgrounds.Select(option => option.Value));
        Assert.Equal(
            _texts.GetText(SettingsTextKeys.ForOverlayBackground(OverlayBackground.Image)),
            _viewModel.OverlayBackgrounds[1].Label);
    }

    [Fact]
    public async Task WithoutPicture_SaysSo()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);

        _viewModel.BreakOverlayImagePath = null;

        Assert.Equal(_texts.GetText(SettingsTextKeys.PauseActionsOverlayImageNone), _viewModel.OverlayImageName);
    }

    [Fact]
    public async Task UseOverlayImage_TakesThePictureAndSwitchesToPicture()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);
        _viewModel.SelectOverlayBackground(OverlayBackground.Color);

        bool taken = _viewModel.UseOverlayImage(@"D:\Fotos\see.PNG");

        Assert.True(taken);
        Assert.True(_viewModel.IsOverlayImageSelected);
        Assert.Equal(@"D:\Fotos\see.PNG", _viewModel.BreakOverlayImagePath);
    }

    [Fact]
    public async Task UseOverlayImage_WithAnUnsupportedFile_ChangesNothing()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);

        bool taken = _viewModel.UseOverlayImage(@"D:\Fotos\see.gif");

        Assert.False(taken);
        Assert.Equal(Picture, _viewModel.BreakOverlayImagePath);
    }

    [Fact]
    public async Task Save_StoresBackgroundPictureAndDimming()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);
        _ = _viewModel.UseOverlayImage(@"D:\Fotos\see.png");
        _viewModel.BreakOverlayImageDimPercent = 20;

        await _viewModel.SaveCommand.ExecuteAsync(null);

        Settings saved = await _repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(OverlayBackground.Image, saved.BreakOverlayBackground);
        Assert.Equal(@"D:\Fotos\see.png", saved.BreakOverlayImagePath);
        Assert.Equal(20, saved.BreakOverlayImageDimPercent);
    }

    /// <remarks>Der Regler ist begrenzt; ein Wert von außerhalb landet an der Grenze statt im Fehler.</remarks>
    [Fact]
    public async Task Save_KeepsTheDimmingInsideItsLimits()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);
        _viewModel.BreakOverlayImageDimPercent = 95;

        await _viewModel.SaveCommand.ExecuteAsync(null);

        Settings saved = await _repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Settings.MaxOverlayImageDimPercent, saved.BreakOverlayImageDimPercent);
    }

    /// <remarks>Zurück auf „Farbe“ behält den Bildpfad: Ein späterer Wechsel zurück findet ihn wieder.</remarks>
    [Fact]
    public async Task Save_WithColor_KeepsThePictureForLater()
    {
        await _viewModel.LoadAsync(TestContext.Current.CancellationToken);
        _viewModel.SelectOverlayBackground(OverlayBackground.Color);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        Settings saved = await _repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(OverlayBackground.Color, saved.BreakOverlayBackground);
        Assert.Equal(Picture, saved.BreakOverlayImagePath);
    }

    [Fact]
    public void Limits_ComeFromTheSettings()
    {
        Assert.Equal(Settings.MinOverlayImageDimPercent, _viewModel.OverlayImageDimMin);
        Assert.Equal(Settings.MaxOverlayImageDimPercent, _viewModel.OverlayImageDimMax);
    }

    [Fact]
    public void ReportOverlayImagePickFailed_WithoutException_Throws()
        => Assert.Throws<ArgumentNullException>(() => _viewModel.ReportOverlayImagePickFailed(null!));

    [Fact]
    public void ReportOverlayImagePickFailed_OnlyLogs()
    {
        _viewModel.ReportOverlayImagePickFailed(new InvalidOperationException("Dialog gescheitert"));

        Assert.Null(_viewModel.BreakOverlayImagePath);
    }
}
