using System.Globalization;
using LookAway.Core.Enums;

namespace LookAway.Application.Services;

/// <summary>
/// Uebersetzt den Timer-Zustand in die Tray-Darstellung: Icon-Variante und
/// Tooltip-Text. Reine, UI-freie Logik — damit ohne Tray-Control testbar
/// (siehe BRIEF015, "Icon-Wechsel-Logik auf Service-Level").
/// </summary>
/// <remarks>
/// Die Tooltip-Texte sind bewusst kompakt gehalten (Windows-Tooltip-Limit) und
/// noch deutschsprachig hartcodiert; die Lokalisierung folgt in BRIEF010.
/// </remarks>
public sealed class TrayStatusPresenter
{
    private const string DndTooltip = "DND aktiv — Erinnerungen ausgesetzt";
    private const string PausedTooltip = "Timer pausiert";
    private const string IdleTooltip = "Timer gestoppt";

    /// <summary>
    /// Liefert die Icon-Variante fuer den aktuellen Zustand. Ein aktiver
    /// DND-/Vollbild-Modus hat Vorrang vor dem Timer-Zustand.
    /// </summary>
    /// <param name="state">Aktueller Timer-Zustand.</param>
    /// <param name="isDndActive">Sind Erinnerungen aktuell ausgesetzt?</param>
    /// <returns>Die anzuzeigende <see cref="TrayIconVariant"/>.</returns>
    public TrayIconVariant GetIconVariant(TimerState state, bool isDndActive = false)
    {
        if (isDndActive)
        {
            return TrayIconVariant.Disabled;
        }

        return state switch
        {
            TimerState.Working => TrayIconVariant.Working,
            TimerState.OnBreak => TrayIconVariant.OnBreak,
            TimerState.Paused => TrayIconVariant.Paused,
            TimerState.Idle => TrayIconVariant.Paused,
            _ => TrayIconVariant.Paused,
        };
    }

    /// <summary>
    /// Baut den Tooltip-Text fuer den aktuellen Zustand.
    /// </summary>
    /// <param name="state">Aktueller Timer-Zustand.</param>
    /// <param name="remaining">Verbleibende Zeit der laufenden Phase.</param>
    /// <param name="model">Aktives Pausenmodell (fuer die Modell-Zeile).</param>
    /// <param name="isDndActive">Sind Erinnerungen aktuell ausgesetzt?</param>
    /// <returns>Der mehrzeilige Tooltip-Text.</returns>
    public string GetTooltip(
        TimerState state,
        TimeSpan remaining,
        BreakModel model,
        bool isDndActive = false)
    {
        if (isDndActive)
        {
            return DndTooltip;
        }

        return state switch
        {
            TimerState.Working => $"Naechste Pause in {FormatRemaining(remaining)}\nModell: {model}",
            TimerState.OnBreak => $"Pause laeuft ({FormatRemaining(remaining)} verbleibend)",
            TimerState.Paused => PausedTooltip,
            TimerState.Idle => IdleTooltip,
            _ => IdleTooltip,
        };
    }

    /// <summary>
    /// Formatiert eine Restzeit als <c>mm:ss</c> (Minuten koennen &gt; 59 sein,
    /// z. B. <c>90:00</c>). Negative Werte werden auf <c>00:00</c> geklemmt.
    /// </summary>
    /// <param name="remaining">Restzeit.</param>
    /// <returns>Formatierte Zeit.</returns>
    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        int totalMinutes = (int)remaining.TotalMinutes;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalMinutes:D2}:{remaining.Seconds:D2}");
    }
}
