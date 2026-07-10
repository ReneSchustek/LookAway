using LookAway.Core.Entities;

namespace LookAway.App.ViewModels;

/// <summary>
/// Trägt die gerade gespeicherten Einstellungen, damit laufende Dienste
/// (Timer, Idle-/Vollbild-Erkennung) sie sofort übernehmen können.
/// </summary>
internal sealed class SettingsAppliedEventArgs : EventArgs
{
    /// <summary>
    /// Erzeugt die Ereignisdaten mit den gültigen Einstellungen.
    /// </summary>
    /// <param name="settings">Die gespeicherten Einstellungen.</param>
    public SettingsAppliedEventArgs(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
    }

    /// <summary>Die gespeicherten, jetzt gültigen Einstellungen.</summary>
    public Settings Settings { get; }
}
