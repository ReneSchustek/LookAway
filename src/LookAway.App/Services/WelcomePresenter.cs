using System;
using System.Threading.Tasks;
using LookAway.Application.ViewModels;
using LookAway.Core.Entities;
using Microsoft.UI.Dispatching;

namespace LookAway.Services;

/// <summary>
/// Zeigt den First-Run-Wizard und meldet zurueck, ob er abgeschlossen wurde.
/// </summary>
internal sealed class WelcomePresenter
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<WelcomeViewModel> _viewModelFactory;
    private readonly Action<Settings> _onCompleted;

    /// <summary>
    /// Erzeugt den Presenter.
    /// </summary>
    /// <param name="dispatcher">UI-Dispatcher des Hauptfensters.</param>
    /// <param name="viewModelFactory">Erzeugt das Wizard-ViewModel.</param>
    /// <param name="onCompleted">Callback mit der gespeicherten Erstkonfiguration.</param>
    public WelcomePresenter(
        DispatcherQueue dispatcher,
        Func<WelcomeViewModel> viewModelFactory,
        Action<Settings> onCompleted)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(onCompleted);

        _dispatcher = dispatcher;
        _viewModelFactory = viewModelFactory;
        _onCompleted = onCompleted;
    }

    /// <summary>
    /// Zeigt den Wizard. Das Ergebnis ist <c>true</c>, wenn er abgeschlossen
    /// wurde, und <c>false</c>, wenn der Benutzer das Fenster geschlossen hat.
    /// </summary>
    public Task<bool> ShowAsync()
    {
        TaskCompletionSource<bool> completionSource = new();
        _ = _dispatcher.TryEnqueue(() => OpenWindow(completionSource));
        return completionSource.Task;
    }

    private void OpenWindow(TaskCompletionSource<bool> completionSource)
    {
        WelcomeViewModel viewModel = _viewModelFactory();
        bool completed = false;

        viewModel.Completed += (_, e) =>
        {
            completed = true;
            _onCompleted(e.Settings);
        };

        Views.WelcomeWindow window = new(viewModel);
        window.Closed += (_, _) =>
        {
            viewModel.Dispose();
            _ = completionSource.TrySetResult(completed);
        };
        window.Activate();
    }
}
