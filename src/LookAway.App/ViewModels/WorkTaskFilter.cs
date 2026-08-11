namespace LookAway.App.ViewModels;

/// <summary>
/// Filterzustand der Aufgabenliste.
/// </summary>
internal enum WorkTaskFilter
{
    /// <summary>Alle Aufgaben.</summary>
    All,

    /// <summary>Nur die offenen.</summary>
    Open,

    /// <summary>Nur die erledigten.</summary>
    Completed,
}
