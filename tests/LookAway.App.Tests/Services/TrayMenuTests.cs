using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Enums;
using LookAway.Core.Localization;
using LookAway.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Das Tray-Menü ohne Tray-Symbol: was es in welchem Timer-Zustand anbietet, welche Aktion hinter
/// jedem Eintrag steht und dass ein Sprachwechsel die Texte erneuern lässt.
/// </summary>
public sealed class TrayMenuTests : IDisposable
{
    private readonly FakeTimerService _timer = new();
    private readonly FakeLocalizationService _localization = new();
    private readonly List<string> _actions = [];
    private readonly TrayMenu _menu;

    public TrayMenuTests()
        => _menu = new TrayMenu(
            _timer,
            new TrayStatusPresenter(_localization),
            _localization,
            NullLogger.Instance,
            new TrayActions(
                () => _actions.Add("settings"),
                () => _actions.Add("exit"),
                () => _actions.Add("update"),
                () => _actions.Add("break")));

    public void Dispose() => _menu.Dispose();

    /// <remarks>Im Leerlauf gibt es nichts anzuhalten; der Eintrag ist da, aber gesperrt.</remarks>
    [Fact]
    public void Idle_OffersPauseButDisablesIt()
    {
        _timer.State = TimerState.Idle;

        _menu.Refresh();

        Assert.Equal(_localization.GetText(TrayTextKeys.MenuPause), _menu.ToggleText);
        Assert.False(_menu.IsToggleEnabled);
    }

    [Theory]
    [InlineData(TimerState.Working)]
    [InlineData(TimerState.OnBreak)]
    public void RunningTimer_OffersPause(TimerState state)
    {
        _timer.State = state;

        _menu.Refresh();

        Assert.Equal(_localization.GetText(TrayTextKeys.MenuPause), _menu.ToggleText);
        Assert.True(_menu.IsToggleEnabled);
    }

    [Fact]
    public void PausedTimer_OffersResume()
    {
        _timer.State = TimerState.Paused;

        _menu.Refresh();

        Assert.Equal(_localization.GetText(TrayTextKeys.MenuResume), _menu.ToggleText);
        Assert.True(_menu.IsToggleEnabled);
    }

    /// <remarks>Kurztext und Symbol kommen aus dem Status-Presenter, mit Modell, Aufgabe und „Nicht stören“.</remarks>
    [Fact]
    public void Refresh_TakesTooltipAndIconFromTheStatusPresenter()
    {
        TrayStatusPresenter presenter = new(_localization);
        _timer.State = TimerState.Working;
        _timer.Remaining = TimeSpan.FromMinutes(12);
        _menu.ActiveModel = BreakModel.TaskBased;
        _menu.CurrentTask = "Angebot schreiben";
        _menu.IsDndActive = true;

        _menu.Refresh();

        Assert.Equal(
            presenter.GetTooltip(TimerState.Working, TimeSpan.FromMinutes(12), BreakModel.TaskBased, true, "Angebot schreiben"),
            _menu.Tooltip);
        Assert.Equal(presenter.GetIconVariant(TimerState.Working, isDndActive: true), _menu.Variant);
    }

    [Fact]
    public void TogglePause_OnAPausedTimer_Resumes()
    {
        _timer.State = TimerState.Paused;

        _menu.TogglePause();

        Assert.Equal(1, _timer.ResumeCount);
        Assert.Equal(0, _timer.PauseCount);
    }

    [Theory]
    [InlineData(TimerState.Working)]
    [InlineData(TimerState.OnBreak)]
    public void TogglePause_OnARunningTimer_Pauses(TimerState state)
    {
        _timer.State = state;

        _menu.TogglePause();

        Assert.Equal(1, _timer.PauseCount);
        Assert.Equal(0, _timer.ResumeCount);
    }

    [Fact]
    public void TogglePause_WhenIdle_DoesNothing()
    {
        _timer.State = TimerState.Idle;

        _menu.TogglePause();

        Assert.Equal(0, _timer.PauseCount);
        Assert.Equal(0, _timer.ResumeCount);
    }

    /// <remarks>„Über“ zeigt die Angaben in den Einstellungen; ein eigenes Fenster gibt es nicht.</remarks>
    [Fact]
    public void Entries_TriggerTheirActions()
    {
        _menu.OpenSettings();
        _menu.StartBreak();
        _menu.ShowAbout();
        _menu.DownloadUpdate();
        _menu.Exit();

        Assert.Equal(["settings", "break", "settings", "update", "exit"], _actions);
    }

    [Fact]
    public void Texts_FollowTheCurrentLanguage()
    {
        _localization.SetLanguage(Language.English);

        Assert.Equal(_localization.GetText(TrayTextKeys.MenuUpdate), _menu.UpdateText);
        Assert.Equal(_localization.GetText(TrayTextKeys.MenuSettings), _menu.SettingsText);
        Assert.Equal(_localization.GetText(TrayTextKeys.MenuStartBreak), _menu.StartBreakText);
        Assert.Equal(_localization.GetText(TrayTextKeys.MenuAbout), _menu.AboutText);
        Assert.Equal(_localization.GetText(TrayTextKeys.MenuExit), _menu.ExitText);
        Assert.Equal(_localization.GetText(TrayTextKeys.TooltipBackground), _menu.BackgroundTooltip);
    }

    [Fact]
    public void LanguageChange_AsksToRenewTheTexts()
    {
        int renewals = 0;
        _menu.TextsChanged += (_, _) => renewals++;

        _localization.SetLanguage(Language.English);

        Assert.Equal(1, renewals);
    }

    [Fact]
    public void AfterDispose_LanguageChangesAreIgnored()
    {
        int renewals = 0;
        _menu.TextsChanged += (_, _) => renewals++;
        _menu.Dispose();
        _menu.Dispose();

        _localization.SetLanguage(Language.English);

        Assert.Equal(0, renewals);
    }

    [Theory]
    [InlineData(TrayIconVariant.Working, "tray-working.ico")]
    [InlineData(TrayIconVariant.OnBreak, "tray-onbreak.ico")]
    [InlineData(TrayIconVariant.Paused, "tray-paused.ico")]
    [InlineData(TrayIconVariant.Disabled, "tray-disabled.ico")]
    [InlineData((TrayIconVariant)99, "tray-paused.ico")]
    public void EveryVariant_HasAnIconFile(TrayIconVariant variant, string fileName)
        => Assert.Equal(fileName, TrayMenu.GetAssetFileName(variant));
}
