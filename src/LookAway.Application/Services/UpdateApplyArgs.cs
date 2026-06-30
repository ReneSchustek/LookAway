using System.Globalization;

namespace LookAway.Application.Services;

/// <summary>
/// Parameter des Update-Helfer-Modus (<c>--apply-update</c>): die Prozess-ID der
/// zu beendenden Instanz sowie Quell- (Staging) und Zielordner (Programmordner).
/// Kapselt das Erzeugen und Parsen der Kommandozeile, damit beide Seiten dasselbe
/// Format verwenden und die Logik testbar bleibt (das eigentliche Starten der
/// Prozesse liegt in der App-Schicht).
/// </summary>
public readonly record struct UpdateApplyArgs(int Pid, string Source, string Target)
{
    /// <summary>Kommandozeilen-Flag, das den Helfer-Modus auswählt.</summary>
    public const string ApplyFlag = "--apply-update";

    private const string PidArg = "--pid";
    private const string SourceArg = "--source";
    private const string TargetArg = "--target";

    /// <summary>
    /// Baut die Argumentliste für den Helfer-Aufruf (ohne Programmnamen).
    /// </summary>
    /// <returns>Argumente in der Reihenfolge Flag, PID, Quelle, Ziel.</returns>
    public IReadOnlyList<string> ToArgumentList() =>
    [
        ApplyFlag,
        PidArg, Pid.ToString(CultureInfo.InvariantCulture),
        SourceArg, Source,
        TargetArg, Target,
    ];

    /// <summary>
    /// Versucht, eine Kommandozeile als Helfer-Aufruf zu interpretieren.
    /// </summary>
    /// <param name="args">Prozess-Argumente (inkl. Programmname an Index 0).</param>
    /// <param name="result">Geparste Parameter bei Erfolg.</param>
    /// <returns><c>true</c>, wenn ein gültiger Helfer-Aufruf vorliegt.</returns>
    public static bool TryParse(IReadOnlyList<string>? args, out UpdateApplyArgs result)
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

        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case ApplyFlag:
                    hasFlag = true;
                    break;
                case PidArg when i + 1 < args.Count:
                    _ = int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
                    break;
                case SourceArg when i + 1 < args.Count:
                    source = args[++i];
                    break;
                case TargetArg when i + 1 < args.Count:
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

        result = new UpdateApplyArgs(pid, source, target);
        return true;
    }
}
