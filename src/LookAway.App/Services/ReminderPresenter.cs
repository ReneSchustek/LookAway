using System;
using System.Diagnostics.CodeAnalysis;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.App.ViewModels;
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
    // Wird vom Timer-Consumer-Thread gesetzt/gelesen (Show/IsReminderOpen) und im
    // UI-Thread zurückgesetzt (Completed-Handler) — daher volatile für korrekte
    // Sichtbarkeit über Threads hinweg (analog zu BreakOverlayPresenter._isOverlayOpen).
    private volatile bool _isReminderOpen;

    /// <summary>
    /// Erzeugt den Presenter mit dem UI-Dispatcher und der Lokalisierung.
    /// </summary>
    /// <param name="dispatcher">Dispatcher des Hauptfensters.</param>
    /// <param name="localization">Liefert die sprachabhängigen Texte.</param>
    public ReminderPresenter(DispatcherQueue dispatcher, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(localization);
        _dispatcher = dispatcher;
        _localization = localization;
    }

    /// <inheritdoc />
    public bool IsReminderOpen => _isReminderOpen;

    /// <inheritdoc />
    public void Show(BreakModel model, Action<ReminderResult> onResult)
    {
        ArgumentNullException.ThrowIfNull(onResult);

        if (_isReminderOpen)
        {
            return;
        }

        _isReminderOpen = true;

        _ = _dispatcher.TryEnqueue(() => TryShowWindow(model, onResult));
    }

    // Erzeugt das Erinnerungsfenster auf dem UI-Thread. Scheitert der Aufbau, wird das
    // Offen-Flag zurückgesetzt, damit künftige Erinnerungen nicht dauerhaft
    // unterdrückt bleiben; die verpasste Erinnerung gilt als „übersprungen".
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Der Fenster-Aufbau darf die Erinnerungen nie dauerhaft blockieren: bei jedem Fehler wird das Offen-Flag zurückgesetzt und die Erinnerung als übersprungen behandelt.")]
    private void TryShowWindow(BreakModel model, Action<ReminderResult> onResult)
    {
        try
        {
            string hintKey = BreakModelRegistry.GetHintKey(model);
            BreakReminderViewModel viewModel = new(hintKey);
            viewModel.Completed += (_, e) =>
            {
                _isReminderOpen = false;
                onResult(e.ChosenAction);
            };

            Views.BreakReminderWindow window = new(viewModel, _localization);
            window.Activate();
        }
        catch (Exception)
        {
            _isReminderOpen = false;
            onResult(ReminderResult.Skip);
        }
    }
}
