using System;
using LookAway.Core.Entities;
using LookAway.App.ViewModels;
using Microsoft.UI.Dispatching;

namespace LookAway.App.Services;

/// <summary>
/// Öffnet das Settings-Fenster und stellt sicher, dass nie zwei Fenster
/// gleichzeitig offen sind: ein zweiter Aufruf aktiviert das bestehende. Erzeugt
/// das <see cref="Views.SettingsWindow"/> samt ViewModel auf dem UI-Thread und
/// reicht angewendete Einstellungen an einen Callback weiter (Live-Übernahme).
/// </summary>
internal sealed class SettingsPresenter
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<SettingsViewModel> _viewModelFactory;
    private readonly Action<Settings> _onSettingsApplied;
    private readonly Action<bool> _onHotkeyCaptureChanged;
    private Views.SettingsWindow? _window;
    private SettingsViewModel? _viewModel;

    /// <summary>
    /// Erzeugt den Presenter.
    /// </summary>
    /// <param name="dispatcher">UI-Dispatcher des Hauptfensters.</param>
    /// <param name="viewModelFactory">Erzeugt ein frisches Settings-ViewModel.</param>
    /// <param name="onSettingsApplied">Callback bei gespeicherten Einstellungen.</param>
    /// <param name="onHotkeyCaptureChanged">
    /// Callback für Beginn und Ende einer Hotkey-Aufnahme. Während der Aufnahme
    /// müssen die globalen Hotkeys freigegeben sein, sonst fängt Windows genau die
    /// Kombinationen ab, die aufgenommen werden sollen.
    /// </param>
    public SettingsPresenter(
        DispatcherQueue dispatcher,
        Func<SettingsViewModel> viewModelFactory,
        Action<Settings> onSettingsApplied,
        Action<bool> onHotkeyCaptureChanged)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(onSettingsApplied);
        ArgumentNullException.ThrowIfNull(onHotkeyCaptureChanged);

        _dispatcher = dispatcher;
        _viewModelFactory = viewModelFactory;
        _onSettingsApplied = onSettingsApplied;
        _onHotkeyCaptureChanged = onHotkeyCaptureChanged;
    }

    /// <summary>Zeigt das Settings-Fenster (oder aktiviert das bereits offene).</summary>
    public void Show() => _ = _dispatcher.TryEnqueue(() => _ = OpenWindowAsync());

    private async Task OpenWindowAsync()
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        SettingsViewModel viewModel = _viewModelFactory();
        viewModel.SettingsApplied += OnSettingsApplied;
        viewModel.HotkeyCaptureChanged += OnHotkeyCaptureChanged;
        _viewModel = viewModel;
        await viewModel.LoadAsync().ConfigureAwait(true);

        Views.SettingsWindow window = new(viewModel);
        _window = window;
        window.Closed += OnWindowClosed;
        window.Activate();
    }

    private void OnSettingsApplied(object? sender, SettingsAppliedEventArgs e)
        => _onSettingsApplied(e.Settings);

    private void OnHotkeyCaptureChanged(object? sender, bool aktiv)
        => _onHotkeyCaptureChanged(aktiv);

    private void OnWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
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
            _viewModel = null;
        }

        // Bedingungslos das Ende melden: Ob das ViewModel beim Freigeben noch dazu
        // kam, hängt an der Reihenfolge der Closed-Handler. Ein Fenster ohne
        // Hotkeys zurückzulassen wäre der teurere Fehler; erneut zu registrieren
        // kostet nichts, weil dabei der Zustand aus den Einstellungen hergestellt wird.
        _onHotkeyCaptureChanged(false);
    }
}
