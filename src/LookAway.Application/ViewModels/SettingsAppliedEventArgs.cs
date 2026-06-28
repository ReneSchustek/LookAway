using LookAway.Core.Entities;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Traegt die gerade gespeicherten Einstellungen, damit laufende Dienste
/// (Timer, Idle-/Vollbild-Erkennung) sie sofort uebernehmen koennen.
/// </summary>
public sealed class SettingsAppliedEventArgs : EventArgs
{
    /// <summary>
    /// Erzeugt die Ereignisdaten mit den gueltigen Einstellungen.
    /// </summary>
    /// <param name="settings">Die gespeicherten Einstellungen.</param>
    public SettingsAppliedEventArgs(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
    }

    /// <summary>Die gespeicherten, jetzt gueltigen Einstellungen.</summary>
    public Settings Settings { get; }
}
