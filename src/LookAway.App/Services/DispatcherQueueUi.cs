using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Dispatching;

namespace LookAway.App.Services;

/// <summary>Echte Umsetzung von <see cref="IUiDispatcher"/> über den Dispatcher eines Fensters.</summary>
[ExcludeFromCodeCoverage(Justification = "Reicht an die DispatcherQueue von WinUI weiter; ohne Fenster-Thread nicht prüfbar.")]
internal sealed class DispatcherQueueUi : IUiDispatcher
{
    private readonly DispatcherQueue _dispatcher;

    /// <summary>Erzeugt den Dispatcher-Zugang.</summary>
    /// <param name="dispatcher">Dispatcher des Hauptfensters.</param>
    public DispatcherQueueUi(DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public void Post(Action action) => _ = _dispatcher.TryEnqueue(() => action());
}
