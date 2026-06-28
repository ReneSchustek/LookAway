using System;
using LookAway.Application.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.UI.Dispatching;

namespace LookAway.Services;

/// <summary>
/// Zeigt das Pause-Erinnerungsfenster. Stellt sicher, dass nie mehrere
/// Erinnerungen gleichzeitig offen sind (zweite <c>BreakDue</c>-Meldung bei
/// offenem Fenster wird ignoriert).
/// </summary>
internal interface IReminderPresenter
{
    /// <summary>Ist gerade eine Erinnerung sichtbar?</summary>
    bool IsReminderOpen { get; }

    /// <summary>
    /// Zeigt eine Erinnerung fuer das Modell. Bei bereits offener Erinnerung
    /// passiert nichts.
    /// </summary>
    /// <param name="model">Aktives Pausenmodell (fuer den Hinweis-Schluessel).</param>
    /// <param name="onResult">Callback mit der gewaehlten Aktion.</param>
    void Show(BreakModel model, Action<ReminderResult> onResult);
}

/// <summary>
/// WinUI-Implementierung von <see cref="IReminderPresenter"/>: erzeugt das
/// <see cref="Views.BreakReminderWindow"/> auf dem UI-Thread.
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
    /// <param name="localization">Liefert die sprachabhaengigen Texte.</param>
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
