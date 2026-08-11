using LookAway.Core.Enums;
using Microsoft.UI.Xaml;

namespace LookAway.App.Services;

/// <summary>
/// Hält das gewählte Erscheinungsbild und legt es auf ein Fenster.
/// </summary>
/// <remarks>
/// WinUI kennt das Erscheinungsbild nur je Element, nicht anwendungsweit: Gesetzt
/// wird es an der Wurzel des Fensterinhalts, und von dort erben alle Steuerelemente.
/// Deshalb fragt jedes Fenster beim Aufbau hier nach, statt selbst zu entscheiden.
/// Ein bereits offenes Fenster zieht nicht nach — es bekommt das neue Erscheinungsbild
/// beim nächsten Öffnen. Nur das Einstellungsfenster stellt sich sofort um, damit die
/// Wahl dort sichtbar wird.
/// </remarks>
internal sealed class ThemeService
{
    /// <summary>Das aktuell gewählte Erscheinungsbild.</summary>
    public AppTheme Current { get; private set; } = AppTheme.System;

    /// <summary>
    /// Übernimmt die Wahl des Benutzers. Wirkt auf Fenster, die danach aufgebaut werden.
    /// </summary>
    /// <param name="theme">Gewähltes Erscheinungsbild.</param>
    public void SetTheme(AppTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unbekanntes Erscheinungsbild.");
        }

        Current = theme;
    }

    /// <summary>
    /// Legt das aktuelle Erscheinungsbild auf ein Fenster.
    /// </summary>
    /// <param name="window">Fenster, dessen Inhalt umgestellt wird.</param>
    public void Apply(Window window) => Apply(window, Current);

    /// <summary>
    /// Legt ein bestimmtes Erscheinungsbild auf ein Fenster — für die Vorschau in
    /// den Einstellungen, bevor die Wahl gespeichert ist.
    /// </summary>
    /// <param name="window">Fenster, dessen Inhalt umgestellt wird.</param>
    /// <param name="theme">Anzuwendendes Erscheinungsbild.</param>
    public static void Apply(Window window, AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Ohne FrameworkElement als Inhalt gibt es keine Wurzel, an der das
        // Erscheinungsbild hängen könnte — dann bleibt es beim Systemwert.
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = ToElementTheme(theme);
        }
    }

    private static ElementTheme ToElementTheme(AppTheme theme) => theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };
}
