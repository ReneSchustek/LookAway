using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Domain;

/// <summary>
/// Liefert die Standardintervalle pro Pausenmodell. Single source of truth
/// fuer die Default-Werte gemaess <c>context.md</c>.
/// </summary>
public static class BreakModelRegistry
{
    /// <summary>
    /// Liefert das Standardintervall fuer das angegebene Modell.
    /// </summary>
    /// <param name="model">Pausenmodell.</param>
    /// <returns>Default-<see cref="BreakInterval"/> mit Arbeit, Pause und ggf. MaxLimit.</returns>
    public static BreakInterval GetDefault(BreakModel model) => model switch
    {
        BreakModel.ShortBreaks => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(60),
            BreakDuration = TimeSpan.FromMinutes(5),
        },
        BreakModel.ClassicPomodoro => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(25),
            BreakDuration = TimeSpan.FromMinutes(5),
        },
        BreakModel.ModifiedPomodoro => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(50),
            BreakDuration = TimeSpan.FromMinutes(10),
        },
        BreakModel.Ultradian => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(90),
            BreakDuration = TimeSpan.FromMinutes(20),
        },
        BreakModel.PhysicalCounter => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(40),
            BreakDuration = TimeSpan.FromMinutes(2),
        },
        BreakModel.TaskBased => new BreakInterval
        {
            // Manueller Trigger; bis zur User-Aktion gilt die Hoechstgrenze
            // als Arbeitsdauer, damit der Timer nicht endlos laeuft.
            WorkDuration = TimeSpan.FromMinutes(120),
            BreakDuration = TimeSpan.FromMinutes(10),
            MaxLimit = TimeSpan.FromMinutes(120),
        },
        BreakModel.LegalCompliance => new BreakInterval
        {
            WorkDuration = TimeSpan.FromMinutes(120),
            BreakDuration = TimeSpan.FromMinutes(15),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unbekanntes Pausenmodell."),
    };
}
