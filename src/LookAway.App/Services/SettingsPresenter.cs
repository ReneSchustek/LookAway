using LookAway.App.ViewModels;
using LookAway.Core.Entities;

namespace LookAway.App.Services;

/// <summary>
/// Öffnet das Settings-Fenster, und zwar höchstens eines: Ein zweiter Aufruf
/// holt das bestehende nach vorn. Erzeugt das Fenster samt ViewModel auf dem
/// UI-Thread und reicht angewendete Einstellungen an einen Callback weiter
/// (Live-Übernahme).
/// </summary>
internal sealed class SettingsPresenter
{
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<SettingsViewModel> _viewModelFactory;
    private readonly Func<SettingsViewModel, IAppWindow> _windowFactory;
    private readonly Action<Settings> _onSettingsApplied;
    private readonly Action<bool> _onHotkeyCaptureChanged;
    private readonly Action _onTasksChanged;
    private IAppWindow? _window;
    private SettingsViewModel? _viewModel;

    /// <summary>
    /// Erzeugt den Presenter.
    /// </summary>
    /// <param name="dispatcher">Wechselt auf den Fenster-Thread.</param>
    /// <param name="viewModelFactory">Erzeugt ein frisches Settings-ViewModel.</param>
    /// <param name="windowFactory">Legt das Settings-Fenster zum ViewModel an.</param>
    /// <param name="onSettingsApplied">Callback bei gespeicherten Einstellungen.</param>
    /// <param name="onHotkeyCaptureChanged">
    /// Callback für Beginn und Ende einer Hotkey-Aufnahme. Während der Aufnahme
    /// müssen die globalen Hotkeys freigegeben sein, sonst fängt Windows genau die
    /// Kombinationen ab, die aufgenommen werden sollen.
    /// </param>
    /// <param name="onTasksChanged">
    /// Callback, wenn sich an den Aufgaben etwas geändert hat. Danach muss die laufende
    /// Aufgabe neu bestimmt und das Symbol im Infobereich aktualisiert werden.
    /// </param>
    public SettingsPresenter(
        IUiDispatcher dispatcher,
        Func<SettingsViewModel> viewModelFactory,
        Func<SettingsViewModel, IAppWindow> windowFactory,
        Action<Settings> onSettingsApplied,
        Action<bool> onHotkeyCaptureChanged,
        Action onTasksChanged)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(windowFactory);
        ArgumentNullException.ThrowIfNull(onSettingsApplied);
        ArgumentNullException.ThrowIfNull(onHotkeyCaptureChanged);
        ArgumentNullException.ThrowIfNull(onTasksChanged);
        _dispatcher = dispatcher;
        _viewModelFactory = viewModelFactory;
        _windowFactory = windowFactory;
        _onSettingsApplied = onSettingsApplied;
        _onHotkeyCaptureChanged = onHotkeyCaptureChanged;
        _onTasksChanged = onTasksChanged;
    }

    /// <summary>Zeigt das Settings-Fenster (oder aktiviert das bereits offene).</summary>
    public void Show() => _dispatcher.Post(() => _ = OpenWindowAsync());

    /// <summary>Wie <see cref="Show"/>, aber abwartbar und schon auf dem Fenster-Thread.</summary>
    /// <returns>Der Vorgang bis zum Anzeigen des Fensters.</returns>
    public async Task OpenWindowAsync()
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        SettingsViewModel viewModel = _viewModelFactory();
        viewModel.SettingsApplied += OnSettingsApplied;
        viewModel.HotkeyCaptureChanged += OnHotkeyCaptureChanged;
        viewModel.Tasks.TasksChanged += OnTasksChanged;
        _viewModel = viewModel;

        await viewModel.LoadAsync().ConfigureAwait(true);

        IAppWindow window = _windowFactory(viewModel);
        _window = window;
        window.Closed += OnWindowClosed;
        window.Activate();
    }

    private void OnSettingsApplied(object? sender, SettingsAppliedEventArgs e)
        => _onSettingsApplied(e.Settings);

    private void OnHotkeyCaptureChanged(object? sender, bool aktiv)
        => _onHotkeyCaptureChanged(aktiv);

    private void OnTasksChanged(object? sender, EventArgs e) => _onTasksChanged();

    private void OnWindowClosed(object? sender, EventArgs args)
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
        }

        if (_viewModel is not null)
        {
            _viewModel.SettingsApplied -= OnSettingsApplied;
            _viewModel.HotkeyCaptureChanged -= OnHotkeyCaptureChanged;
            _viewModel.Tasks.TasksChanged -= OnTasksChanged;
            _viewModel = null;
        }

        // Bedingungslos das Ende melden: Ob das ViewModel beim Freigeben noch dazu
        // kam, hängt an der Reihenfolge der Closed-Handler. Ein Fenster ohne
        // Hotkeys zurückzulassen wäre der teurere Fehler; erneut zu registrieren
        // kostet nichts, weil dabei der Zustand aus den Einstellungen hergestellt wird.
        _onHotkeyCaptureChanged(false);
    }
}
