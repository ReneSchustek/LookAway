namespace LookAway.App.ViewModels;

/// <summary>
/// Filterzustand der Pausenmodell-Liste.
/// </summary>
internal enum BreakModelFilter
{
    /// <summary>Alle Modelle.</summary>
    All,

    /// <summary>Nur das gerade verwendete Modell.</summary>
    Active,

    /// <summary>Alle außer dem verwendeten Modell.</summary>
    Inactive,
}
