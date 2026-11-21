namespace LookAway.Core.Domain;

/// <summary>
/// Die Befehlszeile, mit der LookAway das Setup einer Aktualisierung startet.
/// </summary>
/// <remarks>
/// Das Setup-Skript (<c>installer/LookAway.iss</c>) wertet <see cref="Relaunch"/> aus. Wer den Wert
/// hier ändert, ändert ihn dort mit; sonst startet LookAway nach einem Update nicht wieder. Das
/// Beenden der laufenden Instanz läuft nicht über die Befehlszeile, sondern über das Ereignis der
/// Einzelinstanz-Sperre.
/// </remarks>
public static class InstallerArguments
{
    /// <summary>Lässt das Setup nach einer stillen Installation LookAway wieder starten.</summary>
    public const string Relaunch = "/RELAUNCH";

    /// <summary>
    /// Argumente des Setups bei einer Aktualisierung: ohne Rückfragen, mit Fortschrittsanzeige,
    /// ohne Neustart von Windows, danach LookAway wieder starten.
    /// </summary>
    public static IReadOnlyList<string> SilentUpdate { get; } = ["/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", Relaunch];
}
