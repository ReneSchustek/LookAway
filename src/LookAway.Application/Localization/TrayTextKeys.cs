namespace LookAway.Application.Localization;

/// <summary>
/// Sprachneutrale Schluessel des Tray-Menues und der Tooltips.
/// </summary>
public static class TrayTextKeys
{
    /// <summary>Menue: "Einstellungen…".</summary>
    public const string MenuSettings = "Tray.Menu.Settings";

    /// <summary>Menue: "Pause jetzt starten".</summary>
    public const string MenuStartBreak = "Tray.Menu.StartBreak";

    /// <summary>Menue: "Pausieren".</summary>
    public const string MenuPause = "Tray.Menu.Pause";

    /// <summary>Menue: "Fortsetzen".</summary>
    public const string MenuResume = "Tray.Menu.Resume";

    /// <summary>Menue: "Ueber LookAway".</summary>
    public const string MenuAbout = "Tray.Menu.About";

    /// <summary>Menue: "Beenden".</summary>
    public const string MenuExit = "Tray.Menu.Exit";

    /// <summary>Menue: "Update herunterladen" (nur bei verfuegbarem Update sichtbar).</summary>
    public const string MenuUpdate = "Tray.Menu.Update";

    /// <summary>Tooltip im Ruhezustand (App laeuft im Hintergrund).</summary>
    public const string TooltipBackground = "Tray.Tooltip.Background";

    /// <summary>Tooltip bei aktivem DND.</summary>
    public const string TooltipDnd = "Tray.Tooltip.Dnd";

    /// <summary>Tooltip bei pausiertem Timer.</summary>
    public const string TooltipPaused = "Tray.Tooltip.Paused";

    /// <summary>Tooltip bei gestopptem Timer.</summary>
    public const string TooltipIdle = "Tray.Tooltip.Idle";

    /// <summary>Tooltip-Zeile "Naechste Pause in {0}".</summary>
    public const string TooltipNextBreak = "Tray.Tooltip.NextBreak";

    /// <summary>Tooltip-Zeile "Modell: {0}".</summary>
    public const string TooltipModel = "Tray.Tooltip.Model";

    /// <summary>Tooltip waehrend der Pause "Pause laeuft ({0} verbleibend)".</summary>
    public const string TooltipOnBreak = "Tray.Tooltip.OnBreak";
}
