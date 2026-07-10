using System;
using System.Diagnostics;
using System.IO;
using LookAway.Core.Services;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Services;

/// <summary>
/// Startet die zwei Prozess-Rollen der automatischen Aktualisierung: den
/// Helfer-Modus (die gestagte EXE ersetzt nach dem Beenden die Programmdateien)
/// und den Neustart der installierten Anwendung. Das Parsen/Erzeugen der
/// Argumente liegt in <see cref="UpdateApplyArgs"/> (App-frei und testbar).
/// </summary>
internal static class UpdateProcess
{
    /// <summary>
    /// Startet die neue (gestagte) EXE im Helfer-Modus, damit sie nach dem Beenden
    /// dieser Instanz die Dateien im Zielordner ersetzt.
    /// </summary>
    /// <param name="stagedExePath">Pfad der EXE im Staging-Ordner.</param>
    /// <param name="args">Helfer-Parameter (PID, Quelle, Ziel).</param>
    public static void StartApply(string stagedExePath, UpdateApplyArgs args)
    {
        ProcessStartInfo psi = new(stagedExePath)
        {
            UseShellExecute = false,
            WorkingDirectory = args.Source,
        };
        foreach (string argument in args.ToArgumentList())
        {
            psi.ArgumentList.Add(argument);
        }

        _ = Process.Start(psi);
    }

    /// <summary>Startet die installierte Anwendung normal.</summary>
    /// <param name="exePath">Pfad der installierten EXE.</param>
    public static void StartApp(string exePath)
    {
        _ = Process.Start(new ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
        });
    }
}
