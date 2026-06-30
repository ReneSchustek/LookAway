using LookAway.Core.Enums;

namespace LookAway.Core.ValueObjects;

/// <summary>
/// Momentaufnahme des laufenden Arbeits-Countdowns. Dient dazu, den Timer über
/// einen Prozess-Neustart <em>innerhalb derselben Windows-Sitzung</em> hinweg
/// fortzusetzen (z. B. bei einer Aktualisierung), ohne ihn auf die volle
/// Arbeitsdauer zurückzusetzen.
/// </summary>
/// <param name="Model">Das beim Speichern aktive Pausenmodell.</param>
/// <param name="WorkRemaining">Verbleibende Arbeitszeit bis zur nächsten Pause.</param>
/// <param name="SessionMarker">
/// Sitzungsmarke (ungefährer Systemstart-Zeitpunkt) zum Erkennen, ob die
/// Wiederherstellung noch zur selben Windows-Sitzung gehört.
/// </param>
public sealed record TimerSnapshot(BreakModel Model, TimeSpan WorkRemaining, DateTimeOffset SessionMarker);
