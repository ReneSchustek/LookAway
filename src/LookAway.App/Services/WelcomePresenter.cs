using LookAway.App.ViewModels;
using LookAway.Core.Entities;

namespace LookAway.App.Services;

/// <summary>
/// Zeigt den First-Run-Wizard und meldet zurück, ob er abgeschlossen wurde.
/// </summary>
internal sealed class WelcomePresenter
{
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<WelcomeViewModel> _viewModelFactory;
    private readonly Func<WelcomeViewModel, IAppWindow> _windowFactory;
    private readonly Action<Settings> _onCompleted;

    /// <summary>
    /// Erzeugt den Presenter.
    /// </summary>
    /// <param name="dispatcher">Wechselt auf den Fenster-Thread.</param>
    /// <param name="viewModelFactory">Erzeugt das Wizard-ViewModel.</param>
    /// <param name="windowFactory">Legt das Wizard-Fenster zum ViewModel an.</param>
    /// <param name="onCompleted">Callback mit der gespeicherten Erstkonfiguration.</param>
    public WelcomePresenter(
        IUiDispatcher dispatcher,
        Func<WelcomeViewModel> viewModelFactory,
        Func<WelcomeViewModel, IAppWindow> windowFactory,
        Action<Settings> onCompleted)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(windowFactory);
        ArgumentNullException.ThrowIfNull(onCompleted);
        _dispatcher = dispatcher;
        _viewModelFactory = viewModelFactory;
        _windowFactory = windowFactory;
        _onCompleted = onCompleted;
    }

    /// <summary>
    /// Zeigt den Wizard. Das Ergebnis ist <c>true</c>, wenn er abgeschlossen
    /// wurde, und <c>false</c>, wenn der Benutzer das Fenster geschlossen hat.
    /// </summary>
    /// <returns>Ob der Wizard abgeschlossen wurde; fertig, sobald das Fenster zu ist.</returns>
    public Task<bool> ShowAsync()
    {
        TaskCompletionSource<bool> completionSource = new();
        _dispatcher.Post(() => OpenWindow(completionSource));
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

        IAppWindow window = _windowFactory(viewModel);
        window.Closed += (_, _) =>
        {
            viewModel.Dispose();
            _ = completionSource.TrySetResult(completed);
        };
        window.Activate();
    }
}
