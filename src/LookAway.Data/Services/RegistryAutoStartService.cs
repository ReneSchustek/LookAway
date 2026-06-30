using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LookAway.Data.Services;

/// <summary>
/// Trägt LookAway über den Registry-Schlüssel
/// <c>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run</c> in den
/// benutzerspezifischen Windows-Autostart ein. Es werden ausschließlich
/// <c>HKCU</c>-Schlüssel verwendet — damit sind keine Administrator-Rechte
/// erforderlich.
/// </summary>
/// <remarks>
/// Der Eintragswert wird als vollständiger, in Anführungszeichen gesetzter
/// Pfad gespeichert (Pfade mit Leerzeichen, z. B. <c>C:\Program Files\…</c>,
/// müssen quotiert sein). <see cref="Enable"/> ist idempotent und korrigiert
/// einen veralteten Pfad selbsttätig.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class RegistryAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DefaultValueName = "LookAway";

    /// <summary>
    /// Startargument für den Autostart. LookAway läuft ohnehin im Tray, das
    /// Argument macht die Absicht im Registry-Eintrag jedoch sichtbar und wird
    /// von der Anwendung toleriert.
    /// </summary>
    private const string StartArgument = "--minimized";

    private const string ReadFailedMessage = "Autostart-Status konnte nicht aus der Registry gelesen werden.";
    private const string WriteFailedMessage = "Autostart-Eintrag konnte nicht in die Registry geschrieben werden.";
    private const string DeleteFailedMessage = "Autostart-Eintrag konnte nicht aus der Registry entfernt werden.";
    private const string PathFailedMessage = "Pfad zur ausführbaren LookAway-Datei konnte nicht ermittelt werden.";

    private readonly string _valueName;
    private readonly Func<string> _executablePathProvider;
    private readonly ILogger<RegistryAutoStartService> _logger;

    /// <summary>
    /// Erzeugt den Dienst mit dem Produktions-Eintragsnamen <c>LookAway</c> und
    /// dem Pfad des aktuell laufenden Prozesses.
    /// </summary>
    /// <param name="logger">Logger für Autostart-Vorgänge.</param>
    public RegistryAutoStartService(ILogger<RegistryAutoStartService> logger)
        : this(DefaultValueName, ResolveCurrentExecutablePath, logger)
    {
    }

    /// <summary>
    /// Erzeugt den Dienst mit explizitem Eintragsnamen und Pfad-Quelle.
    /// Vorgesehen für Integrationstests, die einen eindeutigen Eintragsnamen
    /// nutzen und nach dem Test wieder aufräumen.
    /// </summary>
    /// <param name="valueName">Name des Registry-Werts unter dem Run-Schlüssel.</param>
    /// <param name="executablePathProvider">Liefert den einzutragenden Programmpfad.</param>
    /// <param name="logger">Logger für Autostart-Vorgänge.</param>
    public RegistryAutoStartService(
        string valueName,
        Func<string> executablePathProvider,
        ILogger<RegistryAutoStartService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentNullException.ThrowIfNull(executablePathProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _valueName = valueName;
        _executablePathProvider = executablePathProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled()
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            object? value = runKey?.GetValue(_valueName);
            return value is string entry && !string.IsNullOrWhiteSpace(entry);
        }
        catch (SecurityException ex)
        {
            throw new AutoStartException(ReadFailedMessage, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AutoStartException(ReadFailedMessage, ex);
        }
        catch (IOException ex)
        {
            throw new AutoStartException(ReadFailedMessage, ex);
        }
    }

    /// <inheritdoc />
    public void Enable()
    {
        string desiredEntry = BuildRunCommand(_executablePathProvider());

        try
        {
            using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            object? currentValue = runKey.GetValue(_valueName);
            if (currentValue is string existing
                && string.Equals(existing, desiredEntry, StringComparison.OrdinalIgnoreCase))
            {
                RegistryAutoStartServiceLog.AlreadyCurrent(_logger, _valueName);
                return;
            }

            runKey.SetValue(_valueName, desiredEntry, RegistryValueKind.String);
            RegistryAutoStartServiceLog.Enabled(_logger, _valueName);
        }
        catch (SecurityException ex)
        {
            throw new AutoStartException(WriteFailedMessage, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AutoStartException(WriteFailedMessage, ex);
        }
        catch (IOException ex)
        {
            throw new AutoStartException(WriteFailedMessage, ex);
        }
    }

    /// <inheritdoc />
    public void Disable()
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (runKey is null)
            {
                return;
            }

            runKey.DeleteValue(_valueName, throwOnMissingValue: false);
            RegistryAutoStartServiceLog.Disabled(_logger, _valueName);
        }
        catch (SecurityException ex)
        {
            throw new AutoStartException(DeleteFailedMessage, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AutoStartException(DeleteFailedMessage, ex);
        }
        catch (IOException ex)
        {
            throw new AutoStartException(DeleteFailedMessage, ex);
        }
    }

    /// <summary>
    /// Baut den Registry-Wert: vollständiger Pfad in Anführungszeichen, gefolgt
    /// vom Start-Argument. Anführungszeichen sind Pflicht, da der Pfad
    /// Leerzeichen enthalten kann.
    /// </summary>
    /// <param name="executablePath">Vollständiger Pfad zur ausführbaren Datei.</param>
    /// <returns>Der zu speichernde Befehlszeilen-String.</returns>
    private static string BuildRunCommand(string executablePath)
    {
        return string.Concat("\"", executablePath, "\" ", StartArgument);
    }

    /// <summary>
    /// Ermittelt den Pfad der aktuell laufenden ausführbaren Datei über das
    /// Hauptmodul des Prozesses.
    /// </summary>
    /// <returns>Vollständiger Pfad zur LookAway.App.exe.</returns>
    private static string ResolveCurrentExecutablePath()
    {
        try
        {
            using Process current = Process.GetCurrentProcess();
            string? path = current.MainModule?.FileName;
            if (string.IsNullOrEmpty(path))
            {
                throw new AutoStartException(PathFailedMessage);
            }

            return path;
        }
        catch (InvalidOperationException ex)
        {
            throw new AutoStartException(PathFailedMessage, ex);
        }
        catch (NotSupportedException ex)
        {
            throw new AutoStartException(PathFailedMessage, ex);
        }
        catch (Win32Exception ex)
        {
            throw new AutoStartException(PathFailedMessage, ex);
        }
    }
}
