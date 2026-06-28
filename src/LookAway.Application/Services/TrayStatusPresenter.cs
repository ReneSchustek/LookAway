using System.Globalization;
using LookAway.Application.Localization;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;

namespace LookAway.Application.Services;

/// <summary>
/// Uebersetzt den Timer-Zustand in die Tray-Darstellung: Icon-Variante und
/// Tooltip-Text. Reine, UI-freie Logik — damit ohne Tray-Control testbar.
/// </summary>
/// <remarks>
/// Die Tooltip-Texte sind bewusst kompakt gehalten (Windows-Tooltip-Limit) und
/// werden ueber die Lokalisierung in der aktuellen Sprache aufgeloest.
/// </remarks>
public sealed class TrayStatusPresenter
{
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Erzeugt den Presenter mit der Lokalisierung.
    /// </summary>
    /// <param name="localization">Liefert die sprachabhaengigen Tooltip-Texte.</param>
    public TrayStatusPresenter(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
    }

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
            return _localization.GetText(TrayTextKeys.TooltipDnd);
        }

        return state switch
        {
            TimerState.Working => BuildWorkingTooltip(remaining, model),
            TimerState.OnBreak => Format(TrayTextKeys.TooltipOnBreak, FormatRemaining(remaining)),
            TimerState.Paused => _localization.GetText(TrayTextKeys.TooltipPaused),
            TimerState.Idle => _localization.GetText(TrayTextKeys.TooltipIdle),
            _ => _localization.GetText(TrayTextKeys.TooltipIdle),
        };
    }

    private string BuildWorkingTooltip(TimeSpan remaining, BreakModel model)
    {
        string nextBreak = Format(TrayTextKeys.TooltipNextBreak, FormatRemaining(remaining));
        string modelName = _localization.GetText(SettingsTextKeys.ForModel(model));
        string modelLine = Format(TrayTextKeys.TooltipModel, modelName);
        return $"{nextBreak}\n{modelLine}";
    }

    private string Format(string key, object argument)
        => string.Format(CultureInfo.CurrentCulture, _localization.GetText(key), argument);

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
