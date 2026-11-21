using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;

namespace LookAway.App.Services;

/// <summary>
/// Echte Umsetzung von <see cref="IAppWindow"/>: ein WinUI-Fenster, auf das beim Anlegen das
/// gewählte Erscheinungsbild gelegt wird.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Hüllt ein WinUI-Fenster; das braucht den Fenster-Thread. Wann Presenter Fenster öffnen und aufräumen, prüfen deren Tests über eine Attrappe.")]
internal sealed class WinUiAppWindow : IAppWindow
{
    private readonly Window _window;

    /// <summary>Hüllt das Fenster und legt das Erscheinungsbild darauf.</summary>
    /// <param name="window">Das WinUI-Fenster.</param>
    /// <param name="themeService">Liefert das gewählte Erscheinungsbild.</param>
    public WinUiAppWindow(Window window, ThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(themeService);

        _window = window;
        themeService.Apply(window);
        _window.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? Closed;

    /// <inheritdoc />
    public void Activate() => _window.Activate();
}
