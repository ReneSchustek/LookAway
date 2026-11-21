
namespace LookAway.App.Services;

/// <summary>
/// Ein Fenster der Anwendung aus Sicht der Presenter: Es lässt sich nach vorn holen und meldet,
/// wenn es geschlossen wurde.
/// </summary>
/// <remarks>
/// Die Presenter legen Fenster nur über eine Fabrik an, die dieses Bild liefert. So lässt sich
/// prüfen, wann sie ein Fenster öffnen, wiederverwenden und aufräumen, ohne WinUI-Fenster
/// anzulegen. Die echte Umsetzung ist <see cref="WinUiAppWindow"/>.
/// </remarks>
internal interface IAppWindow
{
    /// <summary>Das Fenster wurde geschlossen.</summary>
    event EventHandler? Closed;

    /// <summary>Zeigt das Fenster und holt es nach vorn.</summary>
    void Activate();
}
