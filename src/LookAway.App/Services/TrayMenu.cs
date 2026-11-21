using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.Core.Services;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// Was das Tray-Symbol zeigt und was seine Menüeinträge auslösen, ohne WinUI: Kurztext und
/// Symbolvariante aus dem Timer-Zustand, Text und Freigabe des Pausieren/Fortsetzen-Eintrags, die
/// Menütexte in der aktuellen Sprache und die Aktion hinter jedem Eintrag.
/// </summary>
/// <remarks>
/// Der <see cref="TrayIconService"/> überträgt diesen Stand nur noch auf Symbol und Kontextmenü.
/// So lässt sich prüfen, was das Menü in welchem Zustand anbietet, ohne ein Tray-Symbol anzulegen.
/// </remarks>
internal sealed class TrayMenu : IDisposable
{
    private readonly ITimerService _timerService;
    private readonly TrayStatusPresenter _statusPresenter;
    private readonly ILocalizationService _localization;
    private readonly ILogger _logger;
    private readonly TrayActions _actions;
    private bool _disposed;

    /// <summary>Erzeugt das Menü und liest den Anfangszustand.</summary>
    /// <param name="timerService">Timer, dessen Zustand das Menü zeigt und steuert.</param>
    /// <param name="statusPresenter">Bildet Zustand auf Kurztext und Symbol ab.</param>
    /// <param name="localization">Quelle der Menütexte; ein Sprachwechsel meldet <see cref="TextsChanged"/>.</param>
    /// <param name="logger">Protokolliert jede Menüaktion.</param>
    /// <param name="actions">Was die Menüeinträge in der Anwendung auslösen.</param>
    public TrayMenu(
        ITimerService timerService,
        TrayStatusPresenter statusPresenter,
        ILocalizationService localization,
        ILogger logger,
        TrayActions actions)
    {
        ArgumentNullException.ThrowIfNull(timerService);
        ArgumentNullException.ThrowIfNull(statusPresenter);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(actions);

        _timerService = timerService;
        _statusPresenter = statusPresenter;
        _localization = localization;
        _logger = logger;
        _actions = actions;

        _localization.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    /// <summary>Die Sprache hat gewechselt; alle Menütexte sind neu zu lesen.</summary>
    public event EventHandler? TextsChanged;

    /// <summary>Ob der Eintrag „Update“ sichtbar ist.</summary>
    public bool IsUpdateAvailable { get; set; }

    /// <summary>Pausenmodell, das der Kurztext nennt.</summary>
    public BreakModel ActiveModel { get; set; } = Settings.DefaultBreakModel;

    /// <summary>Aktuelle Aufgabe, die der Kurztext nennt; <c>null</c> ohne Aufgabe.</summary>
    public string? CurrentTask { get; set; }

    /// <summary>Ob „Nicht stören“ aktiv ist.</summary>
    public bool IsDndActive { get; set; }

    /// <summary>Kurztext, solange der Timer noch keinen Zustand gemeldet hat.</summary>
    public string BackgroundTooltip => _localization.GetText(TrayTextKeys.TooltipBackground);

    /// <summary>Text des Eintrags „Update“.</summary>
    public string UpdateText => _localization.GetText(TrayTextKeys.MenuUpdate);

    /// <summary>Text des Eintrags „Einstellungen“.</summary>
    public string SettingsText => _localization.GetText(TrayTextKeys.MenuSettings);

    /// <summary>Text des Eintrags „Pause jetzt“.</summary>
    public string StartBreakText => _localization.GetText(TrayTextKeys.MenuStartBreak);

    /// <summary>Text des Eintrags „Über“.</summary>
    public string AboutText => _localization.GetText(TrayTextKeys.MenuAbout);

    /// <summary>Text des Eintrags „Beenden“.</summary>
    public string ExitText => _localization.GetText(TrayTextKeys.MenuExit);

    /// <summary>Kurztext zum aktuellen Zustand, nach dem letzten <see cref="Refresh"/>.</summary>
    public string Tooltip { get; private set; } = string.Empty;

    /// <summary>Symbolvariante zum aktuellen Zustand, nach dem letzten <see cref="Refresh"/>.</summary>
    public TrayIconVariant Variant { get; private set; }

    /// <summary>Text des Eintrags Pausieren oder Fortsetzen, je nach Zustand.</summary>
    public string ToggleText { get; private set; } = string.Empty;

    /// <summary>Ob sich der Timer gerade pausieren oder fortsetzen lässt.</summary>
    public bool IsToggleEnabled { get; private set; }

    /// <summary>Liest den Timer-Zustand und leitet Kurztext, Symbol und Umschalt-Eintrag ab.</summary>
    public void Refresh()
    {
        TimerState state = _timerService.State;

        Variant = _statusPresenter.GetIconVariant(state, IsDndActive);
        Tooltip = _statusPresenter.GetTooltip(state, _timerService.Remaining, ActiveModel, IsDndActive, CurrentTask);
        ToggleText = state == TimerState.Paused
            ? _localization.GetText(TrayTextKeys.MenuResume)
            : _localization.GetText(TrayTextKeys.MenuPause);
        IsToggleEnabled = state != TimerState.Idle;
    }

    /// <summary>Dateiname des Symbols zu einer Variante.</summary>
    /// <param name="variant">Symbolvariante.</param>
    /// <returns>Dateiname unter <c>Assets</c>.</returns>
    /// <remarks>
    /// ICO mit unkomprimierten DIB-Frames, nicht PNG: H.NotifyIcon reicht den Datei-Stream an
    /// System.Drawing.Icon weiter, das nur DIB-Icons lesen kann.
    /// </remarks>
    public static string GetAssetFileName(TrayIconVariant variant) => variant switch
    {
        TrayIconVariant.Working => "tray-working.ico",
        TrayIconVariant.OnBreak => "tray-onbreak.ico",
        TrayIconVariant.Paused => "tray-paused.ico",
        TrayIconVariant.Disabled => "tray-disabled.ico",
        _ => "tray-paused.ico",
    };

    /// <summary>Eintrag „Einstellungen“, Linksklick und Doppelklick auf das Symbol.</summary>
    public void OpenSettings()
    {
        TrayIconLog.SettingsRequested(_logger);
        _actions.ShowSettings();
    }

    /// <summary>Eintrag „Pause jetzt“.</summary>
    public void StartBreak()
    {
        TrayIconLog.StartBreakRequested(_logger);
        _actions.StartBreak();
    }

    /// <summary>
    /// Eintrag Pausieren oder Fortsetzen: Ein pausierter Timer läuft weiter, ein arbeitender oder
    /// pausierender wird angehalten. Im Leerlauf geschieht nichts.
    /// </summary>
    public void TogglePause()
    {
        TimerState state = _timerService.State;
        if (state == TimerState.Paused)
        {
            TrayIconLog.ResumeRequested(_logger);
            _timerService.ResumeAfterPause();
        }
        else if (state is TimerState.Working or TimerState.OnBreak)
        {
            TrayIconLog.PauseRequested(_logger);
            _timerService.Pause();
        }
    }

    /// <summary>Eintrag „Über“; die Angaben stehen in den Einstellungen.</summary>
    public void ShowAbout()
    {
        TrayIconLog.AboutRequested(_logger);
        _actions.ShowSettings();
    }

    /// <summary>Eintrag „Update“.</summary>
    public void DownloadUpdate() => _actions.DownloadUpdate();

    /// <summary>Eintrag „Beenden“.</summary>
    public void Exit()
    {
        TrayIconLog.ExitRequested(_logger);
        _actions.Exit();
    }

    /// <summary>Löst das Menü von der Sprachquelle.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => TextsChanged?.Invoke(this, EventArgs.Empty);
}
