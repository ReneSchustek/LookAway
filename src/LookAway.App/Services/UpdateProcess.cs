using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using LookAway.Core.Domain;
using LookAway.Core.ValueObjects;
using LookAway.Data;

namespace LookAway.App.Services;

/// <summary>
/// Echte Umsetzung von <see cref="IUpdateProcess"/>: startet den Helfer-Modus (die gestagte EXE
/// ersetzt nach dem Beenden die Programmdateien), den Neustart der installierten Anwendung und
/// den Browser. Das Parsen und Erzeugen der Helfer-Argumente liegt in
/// <see cref="UpdateApplyArgs"/> (App-frei und testbar).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Startet und beendet echte Prozesse; wann das geschieht, prüft UpdateOrchestratorTests über eine Attrappe.")]
internal sealed class UpdateProcess : IUpdateProcess
{
    /// <inheritdoc />
    public string InstallDirectory => AppContext.BaseDirectory;

    /// <inheritdoc />
    public int CurrentProcessId => Environment.ProcessId;

    /// <inheritdoc />
    public bool IsSetupInstallation => !AppDataLocation.IsPortable();

    /// <inheritdoc />
    /// <remarks>
    /// Ohne „Als Administrator ausführen“: Das Setup erhöht seine Rechte selbst. Nur so kennt es
    /// den ursprünglichen Benutzer und kann LookAway danach ohne Administratorrechte neu starten;
    /// und bricht der Benutzer die Abfrage ab, läuft LookAway einfach weiter.
    /// </remarks>
    public void StartSetup(string setupPath)
    {
        ProcessStartInfo psi = new(setupPath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(setupPath) ?? AppContext.BaseDirectory,
        };
        foreach (string argument in InstallerArguments.SilentUpdate)
        {
            psi.ArgumentList.Add(argument);
        }

        _ = Process.Start(psi);
    }

    /// <inheritdoc />
    public void StartApply(string stagedExePath, UpdateApplyArgs args)
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

    /// <inheritdoc />
    public void StartApp(string exePath)
    {
        _ = Process.Start(new ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
        });
    }

    /// <inheritdoc />
    public void OpenInBrowser(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);

        _ = Process.Start(new ProcessStartInfo(address.ToString()) { UseShellExecute = true });
    }

    /// <inheritdoc />
    public void WaitForExit(int processId, TimeSpan timeout)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            _ = process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Gibt es den Prozess nicht mehr (ArgumentException) oder ist er zwischen Suchen und
            // Warten beendet worden (InvalidOperationException), ist er schlicht schon fort.
        }
    }

    /// <inheritdoc />
    public void Exit() => Environment.Exit(0);
}
