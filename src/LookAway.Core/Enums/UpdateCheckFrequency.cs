namespace LookAway.Core.Enums;

/// <summary>
/// Häufigkeit der Update-Prüfung.
/// </summary>
public enum UpdateCheckFrequency
{
    /// <summary>Bei jedem App-Start.</summary>
    OnStartup,

    /// <summary>Höchstens einmal täglich.</summary>
    Daily,

    /// <summary>Höchstens einmal wöchentlich.</summary>
    Weekly,
}
