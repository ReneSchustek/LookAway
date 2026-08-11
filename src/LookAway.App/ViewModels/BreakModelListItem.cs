using CommunityToolkit.Mvvm.ComponentModel;
using LookAway.Core.Enums;

namespace LookAway.App.ViewModels;

/// <summary>
/// Ein Pausenmodell als Kachel: Name, Intervalle und was daraus entstanden ist.
/// </summary>
/// <remarks>
/// <see cref="IsActive"/> ist veränderlich, weil die Wahl des Benutzers im laufenden
/// Fenster wandert; alles andere steht mit dem Modell fest.
/// </remarks>
internal sealed partial class BreakModelListItem : ObservableObject
{
    /// <summary>Erzeugt den Eintrag.</summary>
    /// <param name="model">Das dargestellte Pausenmodell.</param>
    /// <param name="name">Lokalisierter Anzeigename.</param>
    /// <param name="hint">Was in dieser Pause zu tun ist.</param>
    /// <param name="breakCount">Anzahl der aufgezeichneten Pausen dieses Modells.</param>
    /// <param name="breakCountText">Die Anzahl als lokalisierter Satz.</param>
    public BreakModelListItem(
        BreakModel model,
        string name,
        string hint,
        int breakCount,
        string breakCountText)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(hint);
        ArgumentNullException.ThrowIfNull(breakCountText);
        ArgumentOutOfRangeException.ThrowIfNegative(breakCount);

        Model = model;
        Name = name;
        Hint = hint;
        BreakCount = breakCount;
        BreakCountText = breakCountText;
    }

    /// <summary>Das dargestellte Pausenmodell.</summary>
    public BreakModel Model { get; }

    /// <summary>Lokalisierter Anzeigename.</summary>
    public string Name { get; }

    /// <summary>Was in dieser Pause zu tun ist.</summary>
    public string Hint { get; }

    /// <summary>Anzahl der aufgezeichneten Pausen dieses Modells.</summary>
    public int BreakCount { get; }

    /// <summary>Die Anzahl als lokalisierter Satz.</summary>
    public string BreakCountText { get; }

    /// <summary>Wahr, wenn dieses Modell gerade verwendet wird.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }
}
