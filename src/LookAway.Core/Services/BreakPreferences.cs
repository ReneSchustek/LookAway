using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Services;

/// <summary>
/// Was der Pausen-Ablauf aus den Einstellungen braucht, um Erinnerung und Overlay zu zeigen: Ton,
/// automatischer Pausenstart und Aussehen des Overlays.
/// </summary>
/// <param name="SoundEnabled">Erinnerungston abspielen?</param>
/// <param name="Sound">Gewählter Ton.</param>
/// <param name="SoundVolume">Lautstärke in Prozent.</param>
/// <param name="AutoStartBreakAfter">
/// Verzögerung bis zum automatischen Pausenstart, oder <c>null</c>: Dann bleibt die Erinnerung
/// offen, bis der Benutzer eine Aktion wählt.
/// </param>
/// <param name="OverlayBackdrop">Farbe und gegebenenfalls Bild des Overlays.</param>
/// <param name="DarkenAllScreens">Alle Monitore abdecken statt nur den Hauptmonitor.</param>
internal sealed record BreakPreferences(
    bool SoundEnabled,
    SoundType Sound,
    int SoundVolume,
    TimeSpan? AutoStartBreakAfter,
    OverlayBackdrop OverlayBackdrop,
    bool DarkenAllScreens)
{
    /// <summary>Die Vorgaben neuer Einstellungen; sie gelten bis zum ersten Übernehmen.</summary>
    public static BreakPreferences Default { get; } = From(new Settings());

    /// <summary>Liest die Werte aus den Einstellungen.</summary>
    /// <param name="settings">Aktuelle Einstellungen.</param>
    /// <returns>Die Pausen-Vorlieben.</returns>
    public static BreakPreferences From(Settings settings) => new(
        settings.SoundEnabled,
        settings.ReminderSound,
        settings.SoundVolumePercent,
        settings.AutoStartBreakEnabled ? TimeSpan.FromSeconds(settings.AutoStartBreakSeconds) : null,
        OverlayBackdrop.From(settings),
        settings.DarkenAllScreens);
}
