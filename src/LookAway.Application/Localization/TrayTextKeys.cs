namespace LookAway.Application.Localization;

/// <summary>
/// Sprachneutrale Schlüssel des Tray-Menüs und der Tooltips.
/// </summary>
public static class TrayTextKeys
{
    /// <summary>Menü: "Einstellungen…".</summary>
    public const string MenuSettings = "Tray.Menu.Settings";

    /// <summary>Menü: "Pause jetzt starten".</summary>
    public const string MenuStartBreak = "Tray.Menu.StartBreak";

    /// <summary>Menü: "Pausieren".</summary>
    public const string MenuPause = "Tray.Menu.Pause";

    /// <summary>Menü: "Fortsetzen".</summary>
    public const string MenuResume = "Tray.Menu.Resume";

    /// <summary>Menü: "Über LookAway".</summary>
    public const string MenuAbout = "Tray.Menu.About";

    /// <summary>Menü: "Beenden".</summary>
    public const string MenuExit = "Tray.Menu.Exit";

    /// <summary>Menü: "Update herunterladen" (nur bei verfügbarem Update sichtbar).</summary>
    public const string MenuUpdate = "Tray.Menu.Update";

    /// <summary>Tooltip im Ruhezustand (App läuft im Hintergrund).</summary>
    public const string TooltipBackground = "Tray.Tooltip.Background";

    /// <summary>Tooltip bei aktivem DND.</summary>
    public const string TooltipDnd = "Tray.Tooltip.Dnd";

    /// <summary>Tooltip bei pausiertem Timer.</summary>
    public const string TooltipPaused = "Tray.Tooltip.Paused";

    /// <summary>Tooltip bei gestopptem Timer.</summary>
    public const string TooltipIdle = "Tray.Tooltip.Idle";

    /// <summary>Tooltip-Zeile "Nächste Pause in {0}".</summary>
    public const string TooltipNextBreak = "Tray.Tooltip.NextBreak";

    /// <summary>Tooltip-Zeile "Modell: {0}".</summary>
    public const string TooltipModel = "Tray.Tooltip.Model";

    /// <summary>Tooltip während der Pause "Pause läuft ({0} verbleibend)".</summary>
    public const string TooltipOnBreak = "Tray.Tooltip.OnBreak";
}
