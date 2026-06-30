using LookAway.Core.Enums;

namespace LookAway.Core.Domain;

/// <summary>
/// Entscheidet, ob eine Update-Prüfung gemäß Häufigkeit fällig ist.
/// Reine Logik — ohne Netzwerk testbar.
/// </summary>
public static class UpdateSchedule
{
    /// <summary>
    /// Ermittelt, ob jetzt geprüft werden soll.
    /// </summary>
    /// <param name="frequency">Konfigurierte Häufigkeit.</param>
    /// <param name="lastCheck">Zeitpunkt der letzten Prüfung; <c>null</c> = noch nie.</param>
    /// <param name="now">Aktuelle Zeit.</param>
    /// <returns><c>true</c>, wenn eine Prüfung fällig ist.</returns>
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
