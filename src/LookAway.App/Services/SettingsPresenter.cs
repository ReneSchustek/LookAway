using System;
using LookAway.Application.ViewModels;
using LookAway.Core.Entities;
using Microsoft.UI.Dispatching;

namespace LookAway.Services;

/// <summary>
/// Oeffnet das Settings-Fenster. Stellt sicher, dass nie zwei Fenster
/// gleichzeitig offen sind: ein zweiter Aufruf aktiviert das bestehende.
/// </summary>
internal interface ISettingsPresenter
{
    /// <summary>Zeigt das Settings-Fenster (oder aktiviert das bereits offene).</summary>
    void Show();
}

/// <summary>
/// WinUI-Implementierung von <see cref="ISettingsPresenter"/>: erzeugt das
/// <see cref="Views.SettingsWindow"/> samt ViewModel auf dem UI-Thread und
/// reicht angewendete Einstellungen an einen Callback weiter (Live-Uebernahme).
/// </summary>
internal sealed class SettingsPresenter : ISettingsPresenter
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<SettingsViewModel> _viewModelFactory;
    private readonly Action<Settings> _onSettingsApplied;
    private Views.SettingsWindow? _window;

    /// <summary>
    /// Erzeugt den Presenter.
    /// </summary>
    /// <param name="dispatcher">UI-Dispatcher des Hauptfensters.</param>
    /// <param name="viewModelFactory">Erzeugt ein frisches Settings-ViewModel.</param>
    /// <param name="onSettingsApplied">Callback bei gespeicherten Einstellungen.</param>
    public SettingsPresenter(
        DispatcherQueue dispatcher,
        Func<SettingsViewModel> viewModelFactory,
        Action<Settings> onSettingsApplied)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(onSettingsApplied);

        _dispatcher = dispatcher;
        _viewModelFactory = viewModelFactory;
        _onSettingsApplied = onSettingsApplied;
    }

    /// <inheritdoc />
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
        await viewModel.LoadAsync().ConfigureAwait(true);

        Views.SettingsWindow window = new(viewModel);
        _window = window;
        window.Closed += OnWindowClosed;
        window.Activate();
    }

    private void OnSettingsApplied(object? sender, SettingsAppliedEventArgs e)
        => _onSettingsApplied(e.Settings);

    private void OnWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
        }
    }
}
