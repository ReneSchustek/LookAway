namespace LookAway.Core.Enums;

/// <summary>
/// Visuelle Varianten des Tray-Icons. Spiegeln den aktuellen Zustand der App
/// auf einen Blick wider.
/// </summary>
public enum TrayIconVariant
{
    /// <summary>Arbeitsphase laeuft (Standard, Indigo).</summary>
    Working,

    /// <summary>Pause aktiv (gruener Akzent).</summary>
    OnBreak,

    /// <summary>Timer manuell pausiert oder gestoppt (Grau).</summary>
    Paused,

    /// <summary>Erinnerungen ausgesetzt, z. B. DND-/Vollbild-Modus (Rot).</summary>
    Disabled,
}
