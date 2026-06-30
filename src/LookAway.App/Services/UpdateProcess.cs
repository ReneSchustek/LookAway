using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace LookAway.Services;

/// <summary>
/// Hilfsfunktionen fuer die zwei Prozess-Rollen der automatischen Aktualisierung:
/// den Helfer-Modus (<c>--apply-update</c>), der nach dem Beenden der App die
/// Dateien tauscht, und den Neustart der installierten Anwendung.
/// </summary>
internal static class UpdateProcess
{
    /// <summary>Kommandozeilen-Flag fuer den Helfer-Modus.</summary>
    public const string ApplyFlag = "--apply-update";

    private const string PidArg = "--pid";
    private const string SourceArg = "--source";
    private const string TargetArg = "--target";

    /// <summary>Beschreibt die Parameter des Helfer-Modus.</summary>
    internal readonly record struct ApplyArgs(int Pid, string Source, string Target);

    /// <summary>
    /// Versucht, die Kommandozeile als Helfer-Aufruf zu interpretieren.
    /// </summary>
    /// <param name="args">Prozess-Argumente (inkl. Programmname an Index 0).</param>
    /// <param name="result">Geparste Parameter bei Erfolg.</param>
    /// <returns><c>true</c>, wenn ein gueltiger Helfer-Aufruf vorliegt.</returns>
    public static bool TryParseApply(string[] args, out ApplyArgs result)
    {
        result = default;
        if (args is null)
        {
            return false;
        }

        bool hasFlag = false;
        int pid = 0;
        string source = string.Empty;
        string target = string.Empty;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case ApplyFlag:
                    hasFlag = true;
                    break;
                case PidArg when i + 1 < args.Length:
                    _ = int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
                    break;
                case SourceArg when i + 1 < args.Length:
                    source = args[++i];
                    break;
                case TargetArg when i + 1 < args.Length:
                    target = args[++i];
                    break;
                default:
                    break;
            }
        }

        if (!hasFlag || pid <= 0 || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        result = new ApplyArgs(pid, source, target);
        return true;
    }

    /// <summary>
    /// Startet die neue (gestagte) EXE im Helfer-Modus, damit sie nach dem Beenden
    /// dieser Instanz die Dateien im Zielordner ersetzt.
    /// </summary>
    /// <param name="stagedExePath">Pfad der EXE im Staging-Ordner.</param>
    /// <param name="pid">Prozess-ID der aktuell laufenden Instanz.</param>
    /// <param name="source">Staging-Ordner mit den neuen Dateien.</param>
    /// <param name="target">Zielverzeichnis (Programmordner).</param>
    public static void StartApply(string stagedExePath, int pid, string source, string target)
    {
        ProcessStartInfo psi = new(stagedExePath)
        {
            UseShellExecute = false,
            WorkingDirectory = source,
        };
        psi.ArgumentList.Add(ApplyFlag);
        psi.ArgumentList.Add(PidArg);
        psi.ArgumentList.Add(pid.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(SourceArg);
        psi.ArgumentList.Add(source);
        psi.ArgumentList.Add(TargetArg);
        psi.ArgumentList.Add(target);

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
