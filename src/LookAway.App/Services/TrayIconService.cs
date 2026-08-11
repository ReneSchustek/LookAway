using System;
using System.Collections.Generic;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LookAway.App.Services;

/// <summary>
/// Stellt das LookAway-Tray-Icon mit Kontextmenü bereit. Vereint die
/// Tray-Bedienung mit den Domain-Operationen über den
/// <see cref="ITimerService"/>.
/// </summary>
/// <remarks>
/// WinUI 3 bringt kein natives Tray-API mit; <c>H.NotifyIcon.WinUI</c>
/// liefert das <see cref="TaskbarIcon"/>-Control.
/// </remarks>
internal sealed class TrayIconService : ITrayController, IDisposable
{
    private const int TooltipRefreshIntervalSeconds = 1;

    private readonly ITimerService _timerService;
    private readonly TrayStatusPresenter _statusPresenter;
    private readonly ILocalizationService _localization;
    private readonly ILogger<TrayIconService> _logger;
    private readonly DispatcherQueue _dispatcher;
    private readonly Action _showSettingsHandler;
    private readonly Action _exitHandler;
    private readonly Action _updateDownloadHandler;
    private readonly Action _startBreakHandler;
    private readonly Dictionary<TrayIconVariant, BitmapImage> _iconCache = new();

    private TaskbarIcon? _icon;
    private MenuFlyoutItem? _updateItem;
    private MenuFlyoutItem? _settingsItem;
    private MenuFlyoutItem? _startBreakItem;
    private MenuFlyoutItem? _toggleItem;
    private MenuFlyoutItem? _aboutItem;
    private MenuFlyoutItem? _exitItem;
    private DispatcherQueueTimer? _statusTimer;
    private TrayIconVariant? _currentVariant;
    private BreakModel _activeModel = BreakModel.ClassicPomodoro;
    private string? _currentTask;
    private bool _isDndActive;
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Service mit den nötigen Abhängigkeiten.
    /// </summary>
    /// <param name="timerService">Domain-Service für Pause/Resume.</param>
    /// <param name="statusPresenter">Übersetzt den Timer-Zustand in Icon und Tooltip.</param>
    /// <param name="localization">Liefert die sprachabhängigen Menü- und Tooltip-Texte.</param>
    /// <param name="dispatcher">UI-Dispatcher des Hauptfensters.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="showSettingsHandler">Callback für Settings-Klick (zeigt das Hauptfenster).</param>
    /// <param name="exitHandler">Callback für "Beenden".</param>
    /// <param name="updateDownloadHandler">Callback für "Update herunterladen".</param>
    /// <param name="startBreakHandler">Callback für "Pause jetzt starten".</param>
    public TrayIconService(
        ITimerService timerService,
        TrayStatusPresenter statusPresenter,
        ILocalizationService localization,
        DispatcherQueue dispatcher,
        ILogger<TrayIconService> logger,
        Action showSettingsHandler,
        Action exitHandler,
        Action updateDownloadHandler,
        Action startBreakHandler)
    {
        ArgumentNullException.ThrowIfNull(timerService);
        ArgumentNullException.ThrowIfNull(statusPresenter);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(showSettingsHandler);
        ArgumentNullException.ThrowIfNull(exitHandler);
        ArgumentNullException.ThrowIfNull(updateDownloadHandler);
        ArgumentNullException.ThrowIfNull(startBreakHandler);

        _timerService = timerService;
        _statusPresenter = statusPresenter;
        _localization = localization;
        _dispatcher = dispatcher;
        _logger = logger;
        _showSettingsHandler = showSettingsHandler;
        _exitHandler = exitHandler;
        _updateDownloadHandler = updateDownloadHandler;
        _startBreakHandler = startBreakHandler;

        _localization.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Zeigt oder verbirgt den "Update herunterladen"-Eintrag im Tray-Menü.
    /// </summary>
    /// <param name="available">Ist ein Update verfügbar?</param>
    public void SetUpdateAvailable(bool available)
        => _ = _dispatcher.TryEnqueue(() =>
        {
            if (_updateItem is not null)
            {
                _updateItem.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            }
        });

    /// <summary>
    /// Setzt das aktive Pausenmodell, das im Tooltip angezeigt wird.
    /// </summary>
    /// <param name="model">Aktuelles Pausenmodell.</param>
    public void SetActiveModel(BreakModel model) => _activeModel = model;

    /// <summary>
    /// Übernimmt die Aufgabe, an der gerade gearbeitet wird. Nur beim
    /// aufgabenbasierten Modell steht sie im Kurztext; <c>null</c> blendet sie aus.
    /// </summary>
    /// <param name="task">Text der laufenden Aufgabe oder <c>null</c>.</param>
    public void SetCurrentTask(string? task) => _currentTask = task;

    /// <summary>
    /// Schaltet die DND-Anzeige (ausgesetzte Erinnerungen). Wird von der
    /// Idle-/Vollbild-Erkennung angesteuert.
    /// </summary>
    /// <param name="isDndActive">Sind Erinnerungen aktuell ausgesetzt?</param>
    public void SetDndActive(bool isDndActive) => _isDndActive = isDndActive;

    /// <summary>
    /// Erstellt das Tray-Icon und meldet die Klick-Handler an.
    /// Idempotent: ein zweiter Aufruf ist eine No-Op.
    /// </summary>
    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_icon is not null)
        {
            return;
        }

        _icon = new TaskbarIcon
        {
            ToolTipText = _localization.GetText(TrayTextKeys.TooltipBackground),
            ContextMenuMode = ContextMenuMode.SecondWindow,
            ContextFlyout = BuildContextMenu(),
        };

        _icon.LeftClickCommand = new DelegateCommand(OnLeftClick);
        _icon.DoubleClickCommand = new DelegateCommand(OnSettingsRequested);

        _icon.ForceCreate();

        RefreshStatus();
        StartStatusTimer();

        TrayIconLog.IconShown(_logger);
    }

    /// <summary>
    /// Aktualisiert Icon-Variante und Tooltip anhand des aktuellen
    /// Timer-Zustands. Das Icon wird nur bei einem Variantenwechsel neu gesetzt.
    /// </summary>
    private void RefreshStatus()
    {
        if (_disposed || _icon is null)
        {
            return;
        }

        TimerState state = _timerService.State;
        TimeSpan remaining = _timerService.Remaining;

        TrayIconVariant variant = _statusPresenter.GetIconVariant(state, _isDndActive);
        _icon.ToolTipText = _statusPresenter.GetTooltip(state, remaining, _activeModel, _isDndActive, _currentTask);

        if (_currentVariant != variant)
        {
            _icon.IconSource = GetIcon(variant);
            _currentVariant = variant;
        }

        // Pausieren/Fortsetzen-Eintrag an den aktuellen Zustand angleichen.
        UpdateMenuForState(state);
    }

    private void StartStatusTimer()
    {
        _statusTimer = _dispatcher.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(TooltipRefreshIntervalSeconds);
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
    }

    private BitmapImage GetIcon(TrayIconVariant variant)
    {
        if (_iconCache.TryGetValue(variant, out BitmapImage? cached))
        {
            return cached;
        }

        BitmapImage image = new(new Uri($"ms-appx:///Assets/{GetAssetFileName(variant)}"));
        _iconCache[variant] = image;
        return image;
    }

    // ICO (mit unkomprimierten DIB-Frames), nicht PNG: H.NotifyIcon reicht den
    // Datei-Stream an System.Drawing.Icon weiter, das nur DIB-Icons lesen kann.
    private static string GetAssetFileName(TrayIconVariant variant) => variant switch
    {
        TrayIconVariant.Working => "tray-working.ico",
        TrayIconVariant.OnBreak => "tray-onbreak.ico",
        TrayIconVariant.Paused => "tray-paused.ico",
        TrayIconVariant.Disabled => "tray-disabled.ico",
        _ => "tray-paused.ico",
    };

    /// <summary>
    /// Aktualisiert die Sichtbarkeit der Pause/Fortsetzen-Einträge passend
    /// zum aktuellen <see cref="TimerState"/>.
    /// </summary>
    public void UpdateMenuForState(TimerState state)
    {
        if (_disposed || _toggleItem is null)
        {
            return;
        }

        _ = _dispatcher.TryEnqueue(() =>
        {
            _toggleItem.Text = state == TimerState.Paused
                ? _localization.GetText(TrayTextKeys.MenuResume)
                : _localization.GetText(TrayTextKeys.MenuPause);
            _toggleItem.IsEnabled = state != TimerState.Idle;
        });
    }

    /// <summary>
    /// Entfernt das Tray-Icon und gibt die Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _localization.LanguageChanged -= OnLanguageChanged;

        if (_statusTimer is not null)
        {
            _statusTimer.Stop();
            _statusTimer = null;
        }

        if (_icon is not null)
        {
            try
            {
                _icon.Dispose();
            }
            catch (InvalidOperationException)
            {
                // Tray-API kann beim Shutdown überraschend werfen — tolerieren.
            }
            _icon = null;
        }
    }

    private MenuFlyout BuildContextMenu()
    {
        MenuFlyout flyout = new();

        _updateItem = new MenuFlyoutItem
        {
            Text = _localization.GetText(TrayTextKeys.MenuUpdate),
            Visibility = Visibility.Collapsed,
        };
        _updateItem.Click += (_, _) => _updateDownloadHandler();
        flyout.Items.Add(_updateItem);

        _settingsItem = new MenuFlyoutItem { Text = _localization.GetText(TrayTextKeys.MenuSettings) };
        _settingsItem.Click += (_, _) => OnSettingsRequested();
        flyout.Items.Add(_settingsItem);

        _startBreakItem = new MenuFlyoutItem { Text = _localization.GetText(TrayTextKeys.MenuStartBreak) };
        _startBreakItem.Click += (_, _) => OnStartBreakNow();
        flyout.Items.Add(_startBreakItem);

        _toggleItem = new MenuFlyoutItem { Text = _localization.GetText(TrayTextKeys.MenuPause), IsEnabled = false };
        _toggleItem.Click += (_, _) => OnTogglePause();
        flyout.Items.Add(_toggleItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        _aboutItem = new MenuFlyoutItem { Text = _localization.GetText(TrayTextKeys.MenuAbout) };
        _aboutItem.Click += (_, _) => OnAboutRequested();
        flyout.Items.Add(_aboutItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        _exitItem = new MenuFlyoutItem { Text = _localization.GetText(TrayTextKeys.MenuExit) };
        _exitItem.Click += (_, _) => OnExitRequested();
        flyout.Items.Add(_exitItem);

        return flyout;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
        => _ = _dispatcher.TryEnqueue(RefreshMenuTexts);

    private void RefreshMenuTexts()
    {
        if (_disposed)
        {
            return;
        }

        if (_updateItem is not null)
        {
            _updateItem.Text = _localization.GetText(TrayTextKeys.MenuUpdate);
        }

        if (_settingsItem is not null)
        {
            _settingsItem.Text = _localization.GetText(TrayTextKeys.MenuSettings);
        }

        if (_startBreakItem is not null)
        {
            _startBreakItem.Text = _localization.GetText(TrayTextKeys.MenuStartBreak);
        }

        if (_aboutItem is not null)
        {
            _aboutItem.Text = _localization.GetText(TrayTextKeys.MenuAbout);
        }

        if (_exitItem is not null)
        {
            _exitItem.Text = _localization.GetText(TrayTextKeys.MenuExit);
        }

        // Toggle-Text hängt vom Zustand ab; Tooltip aktualisiert RefreshStatus.
        UpdateMenuForState(_timerService.State);
        RefreshStatus();
    }

    private void OnLeftClick() => OnSettingsRequested();

    private void OnSettingsRequested()
    {
        TrayIconLog.SettingsRequested(_logger);
        _showSettingsHandler();
    }

    private void OnStartBreakNow()
    {
        TrayIconLog.StartBreakRequested(_logger);
        _startBreakHandler();
    }

    private void OnTogglePause()
    {
        if (_timerService.State == TimerState.Paused)
        {
            TrayIconLog.ResumeRequested(_logger);
            _timerService.ResumeAfterPause();
        }
        else if (_timerService.State == TimerState.Working || _timerService.State == TimerState.OnBreak)
        {
            TrayIconLog.PauseRequested(_logger);
            _timerService.Pause();
        }
    }

    private void OnAboutRequested()
    {
        TrayIconLog.AboutRequested(_logger);
        _showSettingsHandler();
    }

    private void OnExitRequested()
    {
        TrayIconLog.ExitRequested(_logger);
        _exitHandler();
    }

    private sealed class DelegateCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;

        public DelegateCommand(Action action) => _action = action;

        // Diese Tray-Befehle sind immer ausführbar; die Ausführbarkeit ändert sich nie.
        // Das von ICommand geforderte Event bleibt daher bewusst ohne Auslösung (leere
        // add/remove-Implementierung statt eines ungenutzten Feld-Events).
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _action();
    }
}
