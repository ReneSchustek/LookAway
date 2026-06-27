using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Domain;

/// <summary>
/// Single source of truth fuer die fachlichen Pausenmodelle: Standardintervalle,
/// erlaubte Anpassungsbereiche und die Lokalisierungs-Schluessel der Hinweise.
/// </summary>
public static class BreakModelRegistry
{
    private const int PhysicalCounterWorkDefaultMinutes = 40;
    private const int PhysicalCounterWorkMinMinutes = 30;
    private const int PhysicalCounterWorkMaxMinutes = 45;
    private const int TaskBasedMaxMinutes = 120;

    /// <summary>
    /// Liefert das Standardintervall fuer das angegebene Modell.
    /// </summary>
    /// <param name="model">Pausenmodell.</param>
    /// <returns>Default-<see cref="BreakInterval"/> mit Arbeit, Pause und ggf. MaxLimit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unbekanntes Modell.</exception>
    public static BreakInterval GetDefault(BreakModel model) => model switch
    {
        BreakModel.ShortBreaks => BreakInterval.Create(
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(5)),
        BreakModel.ClassicPomodoro => BreakInterval.Create(
            TimeSpan.FromMinutes(25),
            TimeSpan.FromMinutes(5)),
        BreakModel.ModifiedPomodoro => BreakInterval.Create(
            TimeSpan.FromMinutes(50),
            TimeSpan.FromMinutes(10)),
        BreakModel.Ultradian => BreakInterval.Create(
            TimeSpan.FromMinutes(90),
            TimeSpan.FromMinutes(20)),
        BreakModel.PhysicalCounter => BreakInterval.Create(
            TimeSpan.FromMinutes(PhysicalCounterWorkDefaultMinutes),
            TimeSpan.FromMinutes(2)),
        BreakModel.TaskBased => BreakInterval.Create(
            // Manueller Trigger; bis zur User-Aktion gilt die Hoechstgrenze
            // als Arbeitsdauer, damit der Timer nicht endlos laeuft.
            TimeSpan.FromMinutes(TaskBasedMaxMinutes),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(TaskBasedMaxMinutes)),
        BreakModel.LegalCompliance => BreakInterval.Create(
            TimeSpan.FromMinutes(120),
            TimeSpan.FromMinutes(15)),
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unbekanntes Pausenmodell."),
    };

    /// <summary>
    /// Liefert das effektive Intervall: die Benutzer-Ueberschreibung, falls
    /// vorhanden, sonst die Modell-Defaults. Das modellspezifische
    /// <see cref="BreakInterval.MaxLimit"/> bleibt erhalten.
    /// </summary>
    /// <param name="model">Pausenmodell.</param>
    /// <param name="customDurations">Optionale Benutzerwerte; <c>null</c> = Defaults.</param>
    /// <returns>Das anzuwendende <see cref="BreakInterval"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unbekanntes Modell oder ungueltige Werte.</exception>
    /// <exception cref="ArgumentException">MaxLimit kleiner als die ueberschriebene Arbeitsdauer.</exception>
    public static BreakInterval GetEffective(BreakModel model, CustomDurations? customDurations)
    {
        BreakInterval defaults = GetDefault(model);
        if (customDurations is null)
        {
            return defaults;
        }

        return BreakInterval.Create(
            TimeSpan.FromMinutes(customDurations.WorkMinutes),
            TimeSpan.FromMinutes(customDurations.BreakMinutes),
            defaults.MaxLimit);
    }

    /// <summary>
    /// Liefert den erlaubten Anpassungsbereich der Arbeitsdauer fuer Modelle mit
    /// begrenzter Konfigurierbarkeit. Aktuell nur <c>PhysicalCounter</c>
    /// (30–45 min); fuer alle anderen Modelle <c>null</c>.
    /// </summary>
    /// <param name="model">Pausenmodell.</param>
    /// <returns>Der Bereich oder <c>null</c>, wenn kein begrenzter Slider vorgesehen ist.</returns>
    public static WorkDurationRange? GetWorkDurationRange(BreakModel model) => model switch
    {
        BreakModel.PhysicalCounter => new WorkDurationRange(
            TimeSpan.FromMinutes(PhysicalCounterWorkMinMinutes),
            TimeSpan.FromMinutes(PhysicalCounterWorkMaxMinutes)),
        _ => null,
    };

    /// <summary>
    /// Liefert den Lokalisierungs-Schluessel des Uebungs-/Pausen-Hinweises fuer
    /// das Modell. <c>ClassicPomodoro</c> und <c>ModifiedPomodoro</c> teilen sich
    /// den Schluessel <see cref="BreakHintKeys.Pomodoro"/>.
    /// </summary>
    /// <param name="model">Pausenmodell.</param>
    /// <returns>Der sprachneutrale Hinweis-Schluessel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unbekanntes Modell.</exception>
    public static string GetHintKey(BreakModel model) => model switch
    {
        BreakModel.ShortBreaks => BreakHintKeys.ShortBreaks,
        BreakModel.ClassicPomodoro => BreakHintKeys.Pomodoro,
        BreakModel.ModifiedPomodoro => BreakHintKeys.Pomodoro,
        BreakModel.Ultradian => BreakHintKeys.Ultradian,
        BreakModel.PhysicalCounter => BreakHintKeys.PhysicalCounter,
        BreakModel.TaskBased => BreakHintKeys.TaskBased,
        BreakModel.LegalCompliance => BreakHintKeys.LegalCompliance,
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unbekanntes Pausenmodell."),
    };
}
