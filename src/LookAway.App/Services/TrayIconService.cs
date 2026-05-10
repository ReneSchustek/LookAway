using System;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.Logging;

namespace LookAway.Services;

/// <summary>
/// Stellt das LookAway-Tray-Icon mit Kontextmenue bereit. Vereint die
/// Tray-Bedienung mit den Domain-Operationen ueber den
/// <see cref="ITimerService"/>.
/// </summary>
/// <remarks>
/// WinUI 3 bringt kein natives Tray-API mit; <c>H.NotifyIcon.WinUI</c>
/// liefert das <see cref="TaskbarIcon"/>-Control.
/// </remarks>
internal sealed class TrayIconService : IDisposable
{
    private readonly ITimerService _timerService;
    private readonly ILogger<TrayIconService> _logger;
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<bool> _showSettingsHandler;
    private readonly Action _exitHandler;

    private TaskbarIcon? _icon;
    private MenuFlyoutItem? _toggleItem;
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Service mit den noetigen Abhaengigkeiten.
    /// </summary>
    /// <param name="timerService">Domain-Service fuer Pause/Resume.</param>
    /// <param name="dispatcher">UI-Dispatcher des Hauptfensters.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="showSettingsHandler">Callback fuer Settings-Klick (zeigt das Hauptfenster).</param>
    /// <param name="exitHandler">Callback fuer "Beenden".</param>
    public TrayIconService(
        ITimerService timerService,
        DispatcherQueue dispatcher,
        ILogger<TrayIconService> logger,
        Func<bool> showSettingsHandler,
        Action exitHandler)
    {
        ArgumentNullException.ThrowIfNull(timerService);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(showSettingsHandler);
        ArgumentNullException.ThrowIfNull(exitHandler);

        _timerService = timerService;
        _dispatcher = dispatcher;
        _logger = logger;
        _showSettingsHandler = showSettingsHandler;
        _exitHandler = exitHandler;
    }

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
            ToolTipText = "LookAway laeuft im Hintergrund",
            ContextMenuMode = ContextMenuMode.SecondWindow,
            ContextFlyout = BuildContextMenu(),
        };

        _icon.LeftClickCommand = new DelegateCommand(OnLeftClick);
        _icon.DoubleClickCommand = new DelegateCommand(OnSettingsRequested);

        _icon.ForceCreate();
        TrayIconLog.IconShown(_logger);
    }

    /// <summary>
    /// Aktualisiert die Sichtbarkeit der Pause/Fortsetzen-Eintraege passend
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
                ? "Fortsetzen"
                : "Pausieren";
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
        if (_icon is not null)
        {
            try
            {
                _icon.Dispose();
            }
            catch (InvalidOperationException)
            {
                // Tray-API kann beim Shutdown ueberraschend werfen — tolerieren.
            }
            _icon = null;
        }
    }

    private MenuFlyout BuildContextMenu()
    {
        MenuFlyout flyout = new();

        MenuFlyoutItem settingsItem = new() { Text = "Einstellungen…" };
        settingsItem.Click += (_, _) => OnSettingsRequested();
        flyout.Items.Add(settingsItem);

        MenuFlyoutItem startBreakItem = new() { Text = "Pause jetzt starten" };
        startBreakItem.Click += (_, _) => OnStartBreakNow();
        flyout.Items.Add(startBreakItem);

        MenuFlyoutItem toggleItem = new() { Text = "Pausieren", IsEnabled = false };
        toggleItem.Click += (_, _) => OnTogglePause();
        flyout.Items.Add(toggleItem);
        _toggleItem = toggleItem;

        flyout.Items.Add(new MenuFlyoutSeparator());

        MenuFlyoutItem aboutItem = new() { Text = "Ueber LookAway" };
        aboutItem.Click += (_, _) => OnAboutRequested();
        flyout.Items.Add(aboutItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        MenuFlyoutItem exitItem = new() { Text = "Beenden" };
        exitItem.Click += (_, _) => OnExitRequested();
        flyout.Items.Add(exitItem);

        return flyout;
    }

    private void OnLeftClick() => OnSettingsRequested();

    private void OnSettingsRequested()
    {
        TrayIconLog.SettingsRequested(_logger);
        _ = _showSettingsHandler();
    }

    private void OnStartBreakNow()
    {
        TrayIconLog.StartBreakRequested(_logger);
        // Konkrete Logik wird in BRIEF005-Folgearbeit (Pause-jetzt-Aktion)
        // an den TimerService angebunden. Aktueller Hook setzt den Service
        // in den OnBreak-Zustand, falls bereits gestartet.
        if (_timerService.State == TimerState.Working || _timerService.State == TimerState.OnBreak)
        {
            _timerService.Pause();
        }
    }

    private void OnTogglePause()
    {
        if (_timerService.State == TimerState.Paused)
        {
            TrayIconLog.ResumeRequested(_logger);
            _timerService.Resume();
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
        _ = _showSettingsHandler();
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

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _action();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
