using System.ComponentModel;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Services;

/// <summary>
/// <see cref="ISetupUpdate"/> über den Update-Installer und den Prozessstart.
/// </summary>
internal sealed class SetupUpdate : ISetupUpdate
{
    private readonly IUpdateInstaller _installer;
    private readonly IUpdateProcess _process;
    private readonly ILogger _logger;

    /// <summary>Erzeugt die Setup-Aktualisierung.</summary>
    /// <param name="installer">Lädt das Setup und prüft seine Signatur.</param>
    /// <param name="process">Kennt die Installationsart und startet das Setup.</param>
    /// <param name="logger">Protokolliert Start und Fehlschlag.</param>
    public SetupUpdate(IUpdateInstaller installer, IUpdateProcess process, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(logger);
        _installer = installer;
        _process = process;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsActive => _process.IsSetupInstallation;

    /// <inheritdoc />
    public bool CanInstall(UpdateInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return IsActive
            ? info.SetupUrl is not null && info.SetupSignatureUrl is not null
            : info.PackageUrl is not null;
    }

    /// <inheritdoc />
    public async Task<bool> PrepareAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        using VerifiedSetup? setup = await _installer.PrepareSetupAsync(info, cancellationToken).ConfigureAwait(false);
        return setup is not null;
    }

    /// <inheritdoc />
    public async Task<bool> StartAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        // Die Sperre der geprüften Datei hält, bis Windows das Setup gestartet hat.
        using VerifiedSetup? setup = await _installer.PrepareSetupAsync(info, cancellationToken).ConfigureAwait(true);
        if (setup is null)
        {
            return false;
        }

        try
        {
            _process.StartSetup(setup.Path);
            SetupUpdateLog.Started(_logger, setup.Version);
            return true;
        }
        // Die Datei lässt sich nicht starten, etwa weil ein Virenscanner sie noch prüft.
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            SetupUpdateLog.StartFailed(_logger, ex, setup.Version);
            return false;
        }
    }
}

/// <summary>
/// Source-generierte Logging-Methoden der Setup-Aktualisierung.
/// </summary>
internal static partial class SetupUpdateLog
{
    [LoggerMessage(EventId = 1174, Level = LogLevel.Information, Message = "Setup für Version {Version} gestartet; es beendet LookAway nach der Rechteabfrage selbst.")]
    public static partial void Started(ILogger logger, string version);

    [LoggerMessage(EventId = 1175, Level = LogLevel.Warning, Message = "Setup für Version {Version} ließ sich nicht starten.")]
    public static partial void StartFailed(ILogger logger, Exception exception, string version);
}
