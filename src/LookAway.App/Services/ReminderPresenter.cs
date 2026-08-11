using System;
using LookAway.App.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace LookAway.App.Services;

/// <summary>
/// WinUI-Implementierung von <see cref="IReminderPresenter"/>: erzeugt das
/// <see cref="Views.BreakReminderWindow"/> auf dem UI-Thread. Stellt sicher, dass
/// nie mehrere Erinnerungen gleichzeitig offen sind (eine zweite Anforderung bei
/// offenem Fenster wird ignoriert).
/// </summary>
internal sealed class ReminderPresenter : IReminderPresenter
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ILocalizationService _localization;
    private readonly ThemeService _themeService;
    private readonly ILogger<ReminderPresenter> _logger;
    // Wird vom Timer-Consumer-Thread gesetzt/gelesen (Show/IsReminderOpen) und im
    // UI-Thread zurückgesetzt (Completed-Handler) — daher volatile für korrekte
    // Sichtbarkeit über Threads hinweg (analog zu BreakOverlayPresenter._isOverlayOpen).
    private volatile bool _isReminderOpen;

    /// <summary>
    /// Erzeugt den Presenter mit dem UI-Dispatcher und der Lokalisierung.
    /// </summary>
    /// <param name="dispatcher">Dispatcher des Hauptfensters.</param>
    /// <param name="localization">Liefert die sprachabhängigen Texte.</param>
    /// <param name="themeService">Liefert das gewählte Erscheinungsbild.</param>
    /// <param name="logger">Protokolliert fehlgeschlagene Fenster-Aufbauten.</param>
    public ReminderPresenter(
        DispatcherQueue dispatcher,
        ILocalizationService localization,
        ThemeService themeService,
        ILogger<ReminderPresenter> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(logger);
        _dispatcher = dispatcher;
        _localization = localization;
        _themeService = themeService;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsReminderOpen => _isReminderOpen;

    /// <inheritdoc />
    public void Show(BreakModel model, TimeSpan? autoStartAfter, Action<ReminderResult> onResult)
    {
        ArgumentNullException.ThrowIfNull(onResult);

        if (_isReminderOpen)
        {
            return;
        }

        _isReminderOpen = true;

        _ = _dispatcher.TryEnqueue(() => TryShowWindow(model, autoStartAfter, onResult));
    }

    // Erzeugt das Erinnerungsfenster auf dem UI-Thread. Scheitert der Aufbau, wird das
    // Offen-Flag zurückgesetzt, damit künftige Erinnerungen nicht dauerhaft
    // unterdrückt bleiben; die verpasste Erinnerung gilt als „übersprungen".
    private void TryShowWindow(BreakModel model, TimeSpan? autoStartAfter, Action<ReminderResult> onResult)
    {
        try
        {
            string hintKey = BreakModelRegistry.GetHintKey(model);
            int? autoStartSeconds = autoStartAfter is { } delay
                ? (int)Math.Ceiling(delay.TotalSeconds)
                : null;
            BreakReminderViewModel viewModel = new(hintKey, autoStartSeconds);
            viewModel.Completed += (_, e) =>
            {
                _isReminderOpen = false;
                onResult(e.ChosenAction);
            };

            Views.BreakReminderWindow window = new(viewModel, _localization);
            _themeService.Apply(window);
            window.Activate();
        }
        catch (Exception ex) when (LogWindowFailure(ex))
        {
            _isReminderOpen = false;
            onResult(ReminderResult.Skip);
        }
    }

    // Der Fenster-Aufbau darf die Erinnerungen nie dauerhaft blockieren. Der Filter
    // protokolliert den Fehler, bevor der Stack abgebaut wird, und lässt den
    // Aufräumpfad laufen; die verpasste Erinnerung gilt als übersprungen.
    private bool LogWindowFailure(Exception exception)
    {
        ReminderPresenterLog.WindowCreationFailed(_logger, exception);
        return true;
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Erinnerungs-Presenters.
/// </summary>
internal static partial class ReminderPresenterLog
{
    [LoggerMessage(EventId = 1810, Level = LogLevel.Error, Message = "Erinnerungsfenster konnte nicht erzeugt werden; Erinnerung gilt als übersprungen.")]
    public static partial void WindowCreationFailed(ILogger logger, Exception exception);
}
