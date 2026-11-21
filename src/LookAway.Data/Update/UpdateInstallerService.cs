using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Update;

/// <summary>
/// Verwaltet die automatische Aktualisierung über die Portable-ZIP eines
/// GitHub-Release: lädt das Paket herunter, entpackt es in einen Staging-Ordner
/// und ersetzt über den Helfer-Prozess die Programmdateien, beim nächsten Start
/// oder, vom Benutzer im Tray angestoßen, sofort. Die reine Datei- und
/// Versionslogik ist hier gekapselt; das Beenden und Neustarten der Prozesse
/// steuert die App-Schicht.
/// </summary>
/// <remarks>
/// Die Arbeit teilen sich drei Klassen: <see cref="PackageStager"/> lädt und prüft das Paket,
/// <see cref="StagingTrust"/> entscheidet, welchen Ordnern vertraut wird, und
/// <see cref="StagedFileSwap"/> tauscht die Dateien mit Rückrollen. Dieser Dienst bündelt sie
/// hinter der Schnittstelle, die App und Helfer kennen.
/// </remarks>
public sealed class UpdateInstallerService : IUpdateInstaller
{
    private static readonly TimeSpan DefaultCopyRetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly PackageStager _stager;
    private readonly SetupStager _setupStager;
    private readonly StagingTrust _trust;
    private readonly StagedFileSwap _swap;

    /// <summary>Erzeugt den Dienst.</summary>
    /// <param name="httpClient">HTTP-Zugriff für den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    public UpdateInstallerService(IHttpGetClient httpClient, ILogger<UpdateInstallerService> logger)
        : this(httpClient, logger, DefaultStagingRoot(), new ReleaseSignatureVerifier())
    {
    }

    /// <summary>Konstruktor mit explizitem Staging-Wurzelpfad (für Tests).</summary>
    /// <param name="httpClient">HTTP-Zugriff für den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="stagingRoot">Wurzelordner für entpackte Update-Pakete.</param>
    public UpdateInstallerService(IHttpGetClient httpClient, ILogger<UpdateInstallerService> logger, string stagingRoot)
        : this(httpClient, logger, stagingRoot, new ReleaseSignatureVerifier())
    {
    }

    /// <summary>Konstruktor mit explizitem Staging-Pfad und Signaturprüfer (für Tests).</summary>
    /// <param name="httpClient">HTTP-Zugriff für den Paket-Download.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="stagingRoot">Wurzelordner für entpackte Update-Pakete.</param>
    /// <param name="signatureVerifier">Prüfer der losgelösten Paket-Signatur.</param>
    /// <param name="copyRetryDelay">
    /// Wartezeit zwischen zwei Kopierversuchen beim Datei-Tausch; <c>null</c> für die Vorgabe
    /// von 500 ms. Tests setzen sie herunter, damit der Rückroll-Pfad nicht Sekunden kostet.
    /// </param>
    public UpdateInstallerService(
        IHttpGetClient httpClient,
        ILogger<UpdateInstallerService> logger,
        string stagingRoot,
        ReleaseSignatureVerifier signatureVerifier,
        TimeSpan? copyRetryDelay = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(signatureVerifier);

        StagingRoot = stagingRoot;
        _stager = new PackageStager(httpClient, signatureVerifier, stagingRoot, logger);
        _setupStager = new SetupStager(httpClient, signatureVerifier, stagingRoot, logger);
        _trust = new StagingTrust(stagingRoot, logger);
        _swap = new StagedFileSwap(logger, copyRetryDelay ?? DefaultCopyRetryDelay);
    }

    /// <summary>Wurzelordner, unter dem Update-Pakete je Version entpackt werden.</summary>
    public string StagingRoot { get; }

    /// <summary>Pfad der ausführbaren Datei innerhalb eines Staging-Ordners.</summary>
    /// <param name="directory">Staging- oder Programmordner.</param>
    /// <returns>Der volle Pfad der Programmdatei.</returns>
    public static string ExecutablePathIn(string directory) => UpdateFiles.ExecutablePathIn(directory);

    /// <summary>Berechnet den SHA-256 (Hex, Kleinbuchstaben) einer Datei.</summary>
    /// <param name="path">Dateipfad.</param>
    /// <returns>Hex-Hash.</returns>
    public static string ComputeFileHash(string path) => UpdateFiles.ComputeFileHash(path);

    /// <summary>Prüft, ob in das Verzeichnis geschrieben werden darf (Probe-Datei).</summary>
    /// <param name="directory">Zu prüfendes Verzeichnis.</param>
    /// <returns><c>true</c>, wenn beschreibbar.</returns>
    public static bool IsDirectoryWritable(string directory) => UpdateFiles.IsDirectoryWritable(directory);

    /// <summary>
    /// Prüft, ob ein Staging-Ordner vertrauenswürdig ist: Er muss innerhalb des
    /// bekannten Staging-Wurzelordners liegen (kein untergeschobener Fremdpfad) und
    /// seine Programmdatei muss den erwarteten, zuvor signaturgeprüft vermerkten
    /// SHA-256 tragen. Der Helfer-Prozess ruft dies auf, bevor er Dateien ersetzt —
    /// so wird die Signaturprüfung des Hauptprozesses auch im Helfer durchgesetzt.
    /// </summary>
    /// <param name="sourceDir">Zu prüfender Quell-/Staging-Ordner.</param>
    /// <param name="expectedSha256">Erwarteter SHA-256 der Programmdatei (aus den Einstellungen).</param>
    /// <returns><c>true</c>, wenn Ordner und Datei-Hash vertrauenswürdig sind.</returns>
    public bool IsTrustedStagingDirectory(string? sourceDir, string? expectedSha256)
        => _trust.IsTrustedStagingDirectory(sourceDir, expectedSha256);

    /// <summary>
    /// Prüft, ob der Zielordner eine echte Installation ist, die ersetzt werden darf.
    /// Der Helfer läuft aus dem Staging-Ordner und kann sein Ziel deshalb nicht aus dem
    /// eigenen Programmpfad ableiten — er bekommt es übergeben und darf ihm nicht blind
    /// vertrauen.
    /// </summary>
    /// <param name="targetDir">Zu prüfender Zielordner (Programmverzeichnis).</param>
    /// <returns><c>true</c>, wenn der Ordner ersetzt werden darf.</returns>
    public bool IsTrustedTargetDirectory(string? targetDir) => _trust.IsTrustedTargetDirectory(targetDir);

    /// <summary>
    /// Lädt das Portable-Paket der angegebenen Aktualisierung herunter und
    /// entpackt es in einen versionsbenannten Staging-Ordner.
    /// </summary>
    /// <param name="info">Die zu installierende Aktualisierung (mit <see cref="UpdateInfo.PackageUrl"/>).</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Staging-Ordner, Version und Datei-Hash bei Erfolg, sonst <c>null</c>.</returns>
    public Task<StagedUpdate?> DownloadAndStageAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        return _stager.DownloadAndStageAsync(info, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VerifiedSetup?> PrepareSetupAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        return _setupStager.PrepareAsync(info, cancellationToken);
    }

    /// <summary>
    /// Liefert den Staging-Ordner eines ausstehenden Updates, aber nur wenn er zur
    /// vermerkten Version gehört, neuer als die aktuelle Version ist und der Hash
    /// der entpackten Programmdatei mit dem vermerkten übereinstimmt. Schützt
    /// davor, einen untergeschobenen Ordner einzuspielen.
    /// </summary>
    /// <param name="current">Aktuell laufende Version.</param>
    /// <param name="expectedVersion">Vermerkte Version des ausstehenden Updates.</param>
    /// <param name="expectedSha256">Vermerkter SHA-256 der Programmdatei.</param>
    /// <returns>Staging-Ordner, oder <c>null</c>, wenn keiner sicher passt.</returns>
    public string? FindVerifiedPendingUpdateDirectory(Version current, string? expectedVersion, string? expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(current);
        return _trust.FindVerifiedPendingUpdateDirectory(current, expectedVersion, expectedSha256);
    }

    /// <summary>
    /// Kopiert die Dateien eines Staging-Ordners über das Zielverzeichnis. Die
    /// Portable-Markierung wird ausgelassen, damit eine installierte Version nicht
    /// versehentlich in den Portable-Modus wechselt; vorhandene Benutzerdaten
    /// bleiben erhalten (es wird nur überschrieben, nie gelöscht).
    /// </summary>
    /// <param name="sourceDir">Staging-Ordner mit den neuen Dateien.</param>
    /// <param name="targetDir">Zielverzeichnis (Programmordner).</param>
    /// <param name="cancellationToken">Abbruch; ein Abbruch rollt wie jeder Fehler zurück.</param>
    /// <returns>Der Tausch; bei einem Fehler nach dem Rückrollen die Ausnahme.</returns>
    public Task ApplyStagedFilesAsync(string sourceDir, string targetDir, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDir);
        return _swap.ApplyAsync(sourceDir, targetDir, cancellationToken);
    }

    /// <summary>Entfernt Staging-Ordner, die nicht neuer als die aktuelle Version sind.</summary>
    /// <param name="current">Aktuell laufende Version.</param>
    public void CleanObsolete(Version current)
    {
        ArgumentNullException.ThrowIfNull(current);
        _trust.CleanObsolete(current);
    }

    private static string DefaultStagingRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppPaths.AppFolderName,
            "updates");
}

/// <summary>Source-generierte Logging-Methoden des Update-Installers.</summary>
internal static partial class UpdateInstallerLog
{
    [LoggerMessage(EventId = 1650, Level = LogLevel.Information, Message = "Update {Version} entpackt nach {Directory}.")]
    public static partial void Staged(ILogger logger, string version, string directory);

    [LoggerMessage(EventId = 1651, Level = LogLevel.Warning, Message = "Update {Version} konnte nicht heruntergeladen/entpackt werden.")]
    public static partial void StageFailed(ILogger logger, Exception exception, string version);

    [LoggerMessage(EventId = 1652, Level = LogLevel.Warning, Message = "Entpacktes Paket in {Directory} ist unvollständig (keine EXE).")]
    public static partial void PackageInvalid(ILogger logger, string directory);

    [LoggerMessage(EventId = 1653, Level = LogLevel.Warning, Message = "Staging-Ordner konnte nicht durchsucht werden.")]
    public static partial void ScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1654, Level = LogLevel.Information, Message = "Update-Dateien von {Source} nach {Target} kopiert.")]
    public static partial void Applied(ILogger logger, string source, string target);

    [LoggerMessage(EventId = 1655, Level = LogLevel.Debug, Message = "Kopieren von {Target} erneut versucht (Versuch {Attempt}).")]
    public static partial void CopyRetry(ILogger logger, string target, int attempt);

    [LoggerMessage(EventId = 1656, Level = LogLevel.Error, Message = "Rückrollen von {Target} nach fehlgeschlagenem Update misslungen.")]
    public static partial void RollbackFailed(ILogger logger, Exception exception, string target);

    [LoggerMessage(EventId = 1657, Level = LogLevel.Warning, Message = "Ausstehendes Update in {Directory} abgelehnt: Datei-Hash stimmt nicht mit dem vermerkten überein.")]
    public static partial void HashMismatch(ILogger logger, string directory);

    [LoggerMessage(EventId = 1661, Level = LogLevel.Error, Message = "Update-Helfer abgelehnt: Quellordner {Source} liegt außerhalb des Staging-Bereichs.")]
    public static partial void UntrustedSource(ILogger logger, string source);

    [LoggerMessage(EventId = 1662, Level = LogLevel.Error, Message = "Update-Helfer abgelehnt: Zielordner {Target} ist keine beschreibbare Installation.")]
    public static partial void UntrustedTarget(ILogger logger, string target);

    [LoggerMessage(EventId = 1658, Level = LogLevel.Warning, Message = "Update {Version} abgelehnt: keine losgelöste Signatur vorhanden oder ladbar.")]
    public static partial void SignatureMissing(ILogger logger, string version);

    [LoggerMessage(EventId = 1659, Level = LogLevel.Error, Message = "Update {Version} abgelehnt: Signatur passt nicht zum Paket (eingebetteter Schlüssel).")]
    public static partial void SignatureInvalid(ILogger logger, string version);

    [LoggerMessage(EventId = 1660, Level = LogLevel.Information, Message = "Update {Version}: Paket-Signatur erfolgreich geprüft.")]
    public static partial void SignatureVerified(ILogger logger, string version);

    [LoggerMessage(EventId = 1663, Level = LogLevel.Warning, Message = "Update {Version}: Das Release enthält kein Setup mit Signatur.")]
    public static partial void SetupMissing(ILogger logger, string version);

    [LoggerMessage(EventId = 1664, Level = LogLevel.Warning, Message = "Update {Version}: Setup oder Signatur ließen sich nicht laden.")]
    public static partial void SetupDownloadFailed(ILogger logger, string version);

    [LoggerMessage(EventId = 1665, Level = LogLevel.Warning, Message = "Update {Version}: Setup konnte nicht bereitgestellt werden.")]
    public static partial void SetupPrepareFailed(ILogger logger, Exception exception, string version);

    [LoggerMessage(EventId = 1666, Level = LogLevel.Information, Message = "Update {Version}: Setup-Signatur erfolgreich geprüft.")]
    public static partial void SetupVerified(ILogger logger, string version);
}
