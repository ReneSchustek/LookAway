
namespace LookAway.App.Services;

/// <summary>Was die Einträge des Tray-Menüs in der Anwendung auslösen.</summary>
/// <param name="ShowSettings">Öffnet die Einstellungen (auch für „Über“).</param>
/// <param name="Exit">Beendet die Anwendung.</param>
/// <param name="DownloadUpdate">Spielt das gefundene Update ein.</param>
/// <param name="StartBreak">Startet sofort eine Pause.</param>
internal sealed record TrayActions(Action ShowSettings, Action Exit, Action DownloadUpdate, Action StartBreak);
