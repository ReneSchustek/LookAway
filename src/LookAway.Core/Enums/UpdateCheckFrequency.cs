namespace LookAway.Core.Enums;

/// <summary>
/// Haeufigkeit der Update-Pruefung (BRIEF020).
/// </summary>
public enum UpdateCheckFrequency
{
    /// <summary>Bei jedem App-Start.</summary>
    OnStartup,

    /// <summary>Hoechstens einmal taeglich.</summary>
    Daily,

    /// <summary>Hoechstens einmal woechentlich.</summary>
    Weekly,
}
