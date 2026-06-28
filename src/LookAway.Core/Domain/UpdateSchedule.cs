using LookAway.Core.Enums;

namespace LookAway.Core.Domain;

/// <summary>
/// Entscheidet, ob eine Update-Pruefung gemaess Haeufigkeit faellig ist (BRIEF020).
/// Reine Logik — ohne Netzwerk testbar.
/// </summary>
public static class UpdateSchedule
{
    /// <summary>
    /// Ermittelt, ob jetzt geprueft werden soll.
    /// </summary>
    /// <param name="frequency">Konfigurierte Haeufigkeit.</param>
    /// <param name="lastCheck">Zeitpunkt der letzten Pruefung; <c>null</c> = noch nie.</param>
    /// <param name="now">Aktuelle Zeit.</param>
    /// <returns><c>true</c>, wenn eine Pruefung faellig ist.</returns>
    public static bool IsDue(UpdateCheckFrequency frequency, DateTimeOffset? lastCheck, DateTimeOffset now)
    {
        if (lastCheck is not { } last)
        {
            return true;
        }

        return frequency switch
        {
            UpdateCheckFrequency.OnStartup => true,
            UpdateCheckFrequency.Daily => now - last >= TimeSpan.FromDays(1),
            UpdateCheckFrequency.Weekly => now - last >= TimeSpan.FromDays(7),
            _ => true,
        };
    }
}
