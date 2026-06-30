using System;
using LookAway.Application.Coordination;
using LookAway.Application.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;

namespace LookAway.Services;

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
    private bool _isReminderOpen;

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

        _ = _dispatcher.TryEnqueue(() =>
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
        });
    }
}
