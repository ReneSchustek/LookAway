using System.Diagnostics.CodeAnalysis;
using H.NotifyIcon;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LookAway.App.Services;

/// <summary>
/// Stellt das LookAway-Tray-Icon mit Kontextmenü bereit. Was Symbol und Menü zeigen und was die
/// Einträge auslösen, bestimmt <see cref="TrayMenu"/>; dieser Dienst überträgt es auf die
/// Steuerelemente und hält den Kurztext sekundengenau.
/// </summary>
/// <remarks>
/// WinUI 3 bringt kein natives Tray-API mit; <c>H.NotifyIcon.WinUI</c>
/// liefert das <see cref="TaskbarIcon"/>-Control.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Überträgt TrayMenu auf Symbol und Kontextmenü von H.NotifyIcon und WinUI; das braucht den Fenster-Thread. Die Logik prüft TrayMenuTests.")]
internal sealed class TrayIconService : ITrayController, IDisposable
{
    // Der Kurztext zeigt die Restzeit sekundengenau (mm:ss).
    private const int TooltipRefreshIntervalSeconds = 1;

    private readonly TrayMenu _menu;
    private readonly ILogger<TrayIconService> _logger;
    private readonly DispatcherQueue _dispatcher;
    private readonly Dictionary<TrayIconVariant, BitmapImage> _iconCache = new();

    private TaskbarIcon? _icon;
    // Jeder Eintrag mit fester Beschriftung und der Quelle seines Textes; ein Sprachwechsel
    // liest sie neu. Der Umschalt-Eintrag hängt am Zustand und läuft über RefreshStatus.
    private readonly List<(MenuFlyoutItem Item, Func<string> Text)> _labelledItems = [];
    private MenuFlyoutItem? _updateItem;
    private MenuFlyoutItem? _toggleItem;
    private DispatcherQueueTimer? _statusTimer;
    private TrayIconVariant? _currentVariant;
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Service mit den nötigen Abhängigkeiten.
    /// </summary>
    /// <param name="timerService">Domain-Service für Pause/Resume.</param>
    /// <param name="statusPresenter">Übersetzt den Timer-Zustand in Icon und Tooltip.</param>
    /// <param name="localization">Liefert die sprachabhängigen Menü- und Tooltip-Texte.</param>
    /// <param name="dispatcher">UI-Dispatcher des Hauptfensters.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="actions">Was die Menüeinträge in der Anwendung auslösen.</param>
    public TrayIconService(
        ITimerService timerService,
        TrayStatusPresenter statusPresenter,
        ILocalizationService localization,
        DispatcherQueue dispatcher,
        ILogger<TrayIconService> logger,
        TrayActions actions)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _menu = new TrayMenu(timerService, statusPresenter, localization, logger, actions);
        _dispatcher = dispatcher;
        _logger = logger;

        _menu.TextsChanged += OnTextsChanged;
    }

    /// <summary>
    /// Zeigt oder verbirgt den "Update herunterladen"-Eintrag im Tray-Menü.
    /// </summary>
    /// <param name="available">Ist ein Update verfügbar?</param>
    public void SetUpdateAvailable(bool available)
        => _ = _dispatcher.TryEnqueue(() =>
        {
            _menu.IsUpdateAvailable = available;
            if (_updateItem is not null)
            {
                _updateItem.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            }
        });

    /// <summary>
    /// Setzt das aktive Pausenmodell, das im Tooltip angezeigt wird.
    /// </summary>
    /// <param name="model">Aktuelles Pausenmodell.</param>
    public void SetActiveModel(BreakModel model) => _menu.ActiveModel = model;

    /// <summary>
    /// Übernimmt die Aufgabe, an der gerade gearbeitet wird. Nur beim
    /// aufgabenbasierten Modell steht sie im Kurztext; <c>null</c> blendet sie aus.
    /// </summary>
    /// <param name="task">Text der laufenden Aufgabe oder <c>null</c>.</param>
    public void SetCurrentTask(string? task) => _menu.CurrentTask = task;

    /// <summary>
    /// Schaltet die DND-Anzeige (ausgesetzte Erinnerungen). Wird von der
    /// Idle-/Vollbild-Erkennung angesteuert.
    /// </summary>
    /// <param name="isDndActive">Sind Erinnerungen aktuell ausgesetzt?</param>
    public void SetDndActive(bool isDndActive) => _menu.IsDndActive = isDndActive;

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
            ToolTipText = _menu.BackgroundTooltip,
            ContextMenuMode = ContextMenuMode.SecondWindow,
            ContextFlyout = BuildContextMenu(),
        };

        _icon.LeftClickCommand = new DelegateCommand(_menu.OpenSettings);
        _icon.DoubleClickCommand = new DelegateCommand(_menu.OpenSettings);

        _icon.ForceCreate();

        RefreshStatus();
        StartStatusTimer();

        TrayIconLog.IconShown(_logger);
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

        _menu.TextsChanged -= OnTextsChanged;
        _menu.Dispose();

        _statusTimer?.Stop();
        _statusTimer = null;
        DisposeIcon();
    }

    private void DisposeIcon()
    {
        try
        {
            _icon?.Dispose();
        }
        catch (InvalidOperationException)
        {
            // Tray-API kann beim Shutdown überraschend werfen — tolerieren.
        }

        _icon = null;
    }

    // Überträgt Kurztext, Symbol und Umschalt-Eintrag; das Symbol nur bei einem Variantenwechsel.
    private void RefreshStatus()
    {
        if (_disposed || _icon is null)
        {
            return;
        }

        _menu.Refresh();
        _icon.ToolTipText = _menu.Tooltip;

        if (_currentVariant != _menu.Variant)
        {
            _icon.IconSource = GetIcon(_menu.Variant);
            _currentVariant = _menu.Variant;
        }

        if (_toggleItem is not null)
        {
            _toggleItem.Text = _menu.ToggleText;
            _toggleItem.IsEnabled = _menu.IsToggleEnabled;
        }
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

        BitmapImage image = new(new Uri($"ms-appx:///Assets/{TrayMenu.GetAssetFileName(variant)}"));
        _iconCache[variant] = image;
        return image;
    }

    private MenuFlyout BuildContextMenu()
    {
        MenuFlyout flyout = new();

        _updateItem = AddItem(flyout, () => _menu.UpdateText, _menu.DownloadUpdate);
        _updateItem.Visibility = _menu.IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
        _ = AddItem(flyout, () => _menu.SettingsText, _menu.OpenSettings);
        _ = AddItem(flyout, () => _menu.StartBreakText, _menu.StartBreak);

        _toggleItem = new MenuFlyoutItem { Text = _menu.ToggleText, IsEnabled = _menu.IsToggleEnabled };
        _toggleItem.Click += (_, _) => _menu.TogglePause();
        flyout.Items.Add(_toggleItem);

        flyout.Items.Add(new MenuFlyoutSeparator());
        _ = AddItem(flyout, () => _menu.AboutText, _menu.ShowAbout);
        flyout.Items.Add(new MenuFlyoutSeparator());
        _ = AddItem(flyout, () => _menu.ExitText, _menu.Exit);

        return flyout;
    }

    private MenuFlyoutItem AddItem(MenuFlyout flyout, Func<string> text, Action click)
    {
        MenuFlyoutItem item = new() { Text = text() };
        item.Click += (_, _) => click();
        flyout.Items.Add(item);
        _labelledItems.Add((item, text));
        return item;
    }

    private void OnTextsChanged(object? sender, EventArgs e)
        => _ = _dispatcher.TryEnqueue(RefreshMenuTexts);

    private void RefreshMenuTexts()
    {
        if (_disposed)
        {
            return;
        }

        foreach ((MenuFlyoutItem item, Func<string> text) in _labelledItems)
        {
            item.Text = text();
        }

        // Umschalt-Eintrag und Kurztext hängen vom Zustand ab.
        RefreshStatus();
    }

    // Kleinster ICommand für die Klickbefehle des Tray-Symbols: ohne Parameter und
    // immer ausführbar.
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
