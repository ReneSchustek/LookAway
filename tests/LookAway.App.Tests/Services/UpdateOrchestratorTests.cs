using System.ComponentModel;
using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Tests für den <see cref="UpdateOrchestrator"/>: die Prüfung beim Start samt Hintergrund-
/// Staging, die Ein-Klick-Installation aus dem Tray, das Einspielen beim nächsten Start und den
/// Helfer-Ablauf. Prozesse startet keiner der Tests; welche Prozessaktion der Ablauf auslöst,
/// hält eine Attrappe fest.
/// </summary>
public sealed class UpdateOrchestratorTests : IDisposable
{
    private const string ReleasePage = "https://github.com/ReneSchustek/LookAway/releases/tag/v9.0.0";

    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _stagingRoot;
    private readonly string _installDirectory;
    private readonly FakeUpdateProcess _process;
    private readonly PackageServer _server = new();

    public UpdateOrchestratorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lookaway-orchestrator-tests", TestIds.Next().ToString("N"));
        _stagingRoot = Path.Combine(_root, "updates");
        _installDirectory = Path.Combine(_root, "Programm");
        _ = Directory.CreateDirectory(_stagingRoot);
        _ = Directory.CreateDirectory(_installDirectory);
        File.WriteAllText(Path.Combine(_installDirectory, "LookAway.exe"), "alte-exe");
        _process = new FakeUpdateProcess(_installDirectory);
    }

    public void Dispose()
    {
        _server.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private UpdateOrchestrator CreateOrchestrator(
        InMemorySettingsRepository repository,
        Version appVersion,
        IUpdateChecker? checker = null,
        IHttpGetClient? http = null)
        => new(
            new UpdateInstallerService(
                http ?? new FakeHttpGetClient(),
                NullLogger<UpdateInstallerService>.Instance,
                _stagingRoot,
                _server.Verifier,
                copyRetryDelay: TimeSpan.Zero),
            checker ?? new FakeUpdateChecker(),
            repository,
            new FakeClock(Now),
            _process,
            appVersion,
            NullLogger<UpdateOrchestrator>.Instance);

    // Eine neuere Version mit Paket und Signatur, wie sie die Release-Prüfung meldet.
    private static UpdateInfo NewerRelease(string? packageAddress = PackageServer.PackageAddress)
        => UpdateInfo.Create(
            new Version(1, 2, 7),
            "v9.0.0",
            ReleasePage,
            null,
            new ReleaseAssets(packageAddress, PackageServer.SignatureAddress));

    // Legt einen entpackten Staging-Ordner an und liefert den Hash seiner Programmdatei.
    private string StageVersion(string version)
    {
        string directory = Path.Combine(_stagingRoot, version);
        _ = Directory.CreateDirectory(directory);
        string exe = Path.Combine(directory, "LookAway.exe");
        File.WriteAllText(exe, "neue-exe");
        return UpdateInstallerService.ComputeFileHash(exe);
    }

    [Fact]
    public async Task AppliedUpdate_ClearsItsRecordOnTheNextStart()
    {
        // Nach erfolgreichem Einspielen läuft genau die vermerkte Version, ihr Staging-Ordner
        // ist abgeräumt. Bliebe der Vermerk stehen, suchte ihn jeder Start erneut.
        InMemorySettingsRepository repository = new(new Settings
        {
            PendingUpdateVersion = "1.2.7",
            PendingUpdateSha256 = "e32d5edf7f0e7f7e720fef9d03d4cc99bf2c04998abf470d5247536c0bda137e",
        });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        bool applying = await orchestrator.TryApplyPendingUpdateOnStartupAsync();

        Assert.False(applying);
        Settings persisted = await repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Null(persisted.PendingUpdateVersion);
        Assert.Null(persisted.PendingUpdateSha256);
    }

    [Fact]
    public async Task PendingRecord_WithoutStagingFolder_IsDiscarded()
    {
        // Neuere Version vermerkt, aber kein passender Ordner (z. B. manuell gelöscht):
        // unbrauchbar — der Vermerk darf nicht liegen bleiben.
        InMemorySettingsRepository repository = new(new Settings
        {
            PendingUpdateVersion = "9.9.9",
            PendingUpdateSha256 = "0000000000000000000000000000000000000000000000000000000000000000",
        });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.False(await orchestrator.TryApplyPendingUpdateOnStartupAsync());
        Settings persisted = await repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Null(persisted.PendingUpdateVersion);
        Assert.Null(persisted.PendingUpdateSha256);
    }

    [Fact]
    public async Task RegularStart_WithoutRecord_LeavesTheSettingsUntouched()
    {
        InMemorySettingsRepository repository = new(new Settings());
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.False(await orchestrator.TryApplyPendingUpdateOnStartupAsync());
        Assert.Equal(0, repository.SaveCallCount);
    }

    /// <remarks>
    /// Die Prüfung läuft beim Start im Hintergrund. Wird die Anwendung sofort wieder
    /// beendet, baut sich der Container ab, während sie noch läuft — der Vermerk des
    /// Prüfzeitpunkts träfe dann auf eine entsorgte Ablage, und zwar in einem Vorgang,
    /// den niemand mehr beobachtet. Der Abbruch beendet sie vorher.
    /// </remarks>
    [Fact]
    public async Task ShutdownDuringStartupCheck_StopsBeforeWritingAnything()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));
        Settings settings = await repository.LoadAsync(TestContext.Current.CancellationToken);
        using CancellationTokenSource shutdown = new();
        await shutdown.CancelAsync();

        await orchestrator.CheckAtStartupAsync(settings, shutdown.Token);

        Assert.Equal(0, repository.SaveCallCount);
    }

    /// <remarks>
    /// Ohne Abbruch läuft sie durch und hält fest, wann zuletzt geprüft wurde — sonst
    /// prüfte jeder Start erneut.
    /// </remarks>
    [Fact]
    public async Task StartupCheck_RecordsTheTimeOfTheCheck()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));
        Settings settings = await repository.LoadAsync(TestContext.Current.CancellationToken);

        await orchestrator.CheckAtStartupAsync(settings, TestContext.Current.CancellationToken);

        Settings persisted = await repository.LoadAsync(TestContext.Current.CancellationToken);
        _ = Assert.NotNull(persisted.LastUpdateCheck);
    }

    [Fact]
    public async Task StartupCheck_SwitchedOff_DoesNotAsk()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = false });
        FakeUpdateChecker checker = new(NewerRelease());
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), checker);

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.Equal(0, checker.CheckCallCount);
    }

    [Fact]
    public async Task StartupCheck_NotYetDue_DoesNotAsk()
    {
        InMemorySettingsRepository repository = new(new Settings
        {
            UpdateCheckEnabled = true,
            UpdateCheckFrequency = UpdateCheckFrequency.Weekly,
            LastUpdateCheck = Now.AddDays(-1),
        });
        FakeUpdateChecker checker = new(NewerRelease());
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), checker);

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.Equal(0, checker.CheckCallCount);
    }

    /// <remarks>Ein Start ohne Netz ist Alltag, kein Fehler; der Prüfzeitpunkt bleibt dann offen.</remarks>
    [Fact]
    public async Task StartupCheck_Offline_EndsQuietlyWithoutRecordingTheCheck()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        FakeUpdateChecker checker = new() { Failure = new HttpRequestException("offline") };
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), checker);

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task StartupCheck_WhenTheTimeCannotBeRecorded_KeepsGoing()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        repository.SaveFailure = new IOException("gesperrt");
        bool? announced = null;
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()));
        orchestrator.UpdateAvailableChanged = available => announced = available;

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.True(announced);
    }

    [Fact]
    public async Task StartupCheck_WithANewerRelease_AnnouncesItWithoutDownloading()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true, AutoUpdate = false });
        bool? announced = null;
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()), _server);
        orchestrator.UpdateAvailableChanged = available => announced = available;

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.True(announced);
        Assert.Equal(0, _server.DownloadCount);
    }

    /// <remarks>
    /// Mit automatischer Aktualisierung wird das Paket im Hintergrund geladen und geprüft; Version
    /// und Hash werden vermerkt, damit der nächste Start genau dieses Paket einspielt.
    /// </remarks>
    [Fact]
    public async Task StartupCheck_WithAutoUpdate_StagesThePackageAndRecordsIt()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true, AutoUpdate = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()), _server);

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Settings persisted = await repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("9.0.0", persisted.PendingUpdateVersion);
        Assert.Equal(
            UpdateInstallerService.ComputeFileHash(Path.Combine(_stagingRoot, "9.0.0", "LookAway.exe")),
            persisted.PendingUpdateSha256);
    }

    [Fact]
    public async Task StartupCheck_WithAnAlreadyStagedRelease_DoesNotDownloadAgain()
    {
        string sha = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings
        {
            UpdateCheckEnabled = true,
            AutoUpdate = true,
            PendingUpdateVersion = "9.0.0",
            PendingUpdateSha256 = sha,
        });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()), _server);

        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.Equal(0, _server.DownloadCount);
    }

    [Fact]
    public async Task StartupCheck_WhenTheStagingCannotBeRecorded_KeepsGoing()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true, AutoUpdate = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()), _server);
        Settings settings = await repository.LoadAsync(TestContext.Current.CancellationToken);
        repository.SaveFailure = new UnauthorizedAccessException("schreibgeschützt");

        Exception? thrown = await Record.ExceptionAsync(() => orchestrator.CheckAtStartupAsync(settings, TestContext.Current.CancellationToken));

        Assert.Null(thrown);
        Assert.True(Directory.Exists(Path.Combine(_stagingRoot, "9.0.0")));
    }

    [Fact]
    public async Task ManualUpdate_WithAStagedPackage_StartsTheHelperAndAsksToRelaunch()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        bool relaunch = false;
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()), _server);
        orchestrator.RelaunchRequested = () => relaunch = true;
        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await orchestrator.HandleManualUpdateRequestedAsync();

        (string exePath, UpdateApplyArgs args) = Assert.Single(_process.AppliesStarted);
        Assert.Equal(Path.Combine(_stagingRoot, "9.0.0", "LookAway.exe"), exePath);
        Assert.Equal(FakeUpdateProcess.ProcessId, args.Pid);
        Assert.Equal(_installDirectory, args.Target);
        Assert.True(relaunch);
    }

    /// <remarks>Ohne gefundenes Update gibt es auch keine Seite, auf die sich zurückfallen ließe.</remarks>
    [Fact]
    public async Task ManualUpdate_WithoutAKnownRelease_OpensNothing()
    {
        UpdateOrchestrator orchestrator = CreateOrchestrator(new InMemorySettingsRepository(new Settings()), new Version(1, 2, 7));

        await orchestrator.HandleManualUpdateRequestedAsync();

        Assert.Empty(_process.BrowserOpened);
        Assert.Empty(_process.AppliesStarted);
    }

    [Fact]
    public async Task ManualUpdate_WhenTheDownloadFails_OpensTheReleasePage()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()));
        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await orchestrator.HandleManualUpdateRequestedAsync();

        Assert.Equal(new Uri(ReleasePage), Assert.Single(_process.BrowserOpened));
        Assert.Empty(_process.AppliesStarted);
    }

    [Fact]
    public async Task ManualUpdate_WithoutAPackage_OpensTheReleasePage()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease(packageAddress: null)), _server);
        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await orchestrator.HandleManualUpdateRequestedAsync();

        _ = Assert.Single(_process.BrowserOpened);
        Assert.Equal(0, _server.DownloadCount);
    }

    [Fact]
    public async Task ManualUpdate_WhenTheHelperCannotStart_FallsBackToTheReleasePage()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        bool relaunch = false;
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()), _server);
        orchestrator.RelaunchRequested = () => relaunch = true;
        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _process.StartApplyFailure = new Win32Exception("blockiert");

        await orchestrator.HandleManualUpdateRequestedAsync();

        _ = Assert.Single(_process.BrowserOpened);
        Assert.False(relaunch);
    }

    /// <remarks>Fehlt ein Standardbrowser, läuft die Anwendung unbeeindruckt weiter.</remarks>
    [Fact]
    public async Task ManualUpdate_WhenNoBrowserOpens_DoesNotThrow()
    {
        InMemorySettingsRepository repository = new(new Settings { UpdateCheckEnabled = true });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7), new FakeUpdateChecker(NewerRelease()));
        await orchestrator.CheckAtStartupAsync(await repository.LoadAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        _process.BrowserFailure = new Win32Exception("kein Browser");

        Exception? thrown = await Record.ExceptionAsync(orchestrator.HandleManualUpdateRequestedAsync);

        Assert.Null(thrown);
    }

    [Fact]
    public async Task PendingUpdate_OnStartup_StartsTheHelperForTheVerifiedFolder()
    {
        string sha = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.0.0", PendingUpdateSha256 = sha });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.True(await orchestrator.TryApplyPendingUpdateOnStartupAsync());

        (string exePath, UpdateApplyArgs args) = Assert.Single(_process.AppliesStarted);
        Assert.Equal(Path.Combine(_stagingRoot, "9.0.0", "LookAway.exe"), exePath);
        Assert.Equal(_installDirectory, args.Target);
    }

    /// <remarks>Eine Installation für alle Benutzer unter „Programme“ lässt sich nicht selbst ersetzen.</remarks>
    [Fact]
    public async Task PendingUpdate_WithAReadOnlyInstallation_StartsNormally()
    {
        string sha = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.0.0", PendingUpdateSha256 = sha });
        Directory.Delete(_installDirectory, recursive: true);
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.False(await orchestrator.TryApplyPendingUpdateOnStartupAsync());
        Assert.Empty(_process.AppliesStarted);
    }

    [Fact]
    public async Task PendingUpdate_WhenTheHelperCannotStart_StartsNormally()
    {
        string sha = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.0.0", PendingUpdateSha256 = sha });
        _process.StartApplyFailure = new InvalidOperationException("kein Prozess");
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.False(await orchestrator.TryApplyPendingUpdateOnStartupAsync());
    }

    /// <remarks>Lässt sich der erledigte Vermerk nicht löschen, wird er beim nächsten Start erneut verworfen.</remarks>
    [Fact]
    public async Task DiscardedRecord_ThatCannotBeSaved_DoesNotStopTheStart()
    {
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.9.9", PendingUpdateSha256 = "abc" });
        repository.SaveFailure = new IOException("gesperrt");
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(1, 2, 7));

        Assert.False(await orchestrator.TryApplyPendingUpdateOnStartupAsync());
    }

    [Fact]
    public async Task Helper_WithTrustedFolders_ReplacesTheFilesAndRestartsTheApp()
    {
        string sha = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.0.0", PendingUpdateSha256 = sha });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(9, 0, 0));
        UpdateApplyArgs apply = new(FakeUpdateProcess.ProcessId, Path.Combine(_stagingRoot, "9.0.0"), _installDirectory);

        await orchestrator.RunHelperApplyAsync(apply);

        Assert.Equal("neue-exe", await File.ReadAllTextAsync(Path.Combine(_installDirectory, "LookAway.exe"), TestContext.Current.CancellationToken));
        Assert.Equal(FakeUpdateProcess.ProcessId, Assert.Single(_process.WaitedFor));
        Assert.Equal(Path.Combine(_installDirectory, "LookAway.exe"), Assert.Single(_process.AppsStarted));
        Assert.Equal(1, _process.ExitCount);
    }

    /// <remarks>
    /// Der Helfer vertraut seinen Argumenten nicht. Eine Quelle, deren Programmdatei nicht den
    /// vermerkten Hash trägt, spielt er nicht ein — startet aber die vorhandene Installation wieder.
    /// </remarks>
    [Fact]
    public async Task Helper_WithAnUntrustedSource_LeavesTheInstallationAndRestartsIt()
    {
        _ = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.0.0", PendingUpdateSha256 = "abc" });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(9, 0, 0));
        UpdateApplyArgs apply = new(FakeUpdateProcess.ProcessId, Path.Combine(_stagingRoot, "9.0.0"), _installDirectory);

        await orchestrator.RunHelperApplyAsync(apply);

        Assert.Equal("alte-exe", await File.ReadAllTextAsync(Path.Combine(_installDirectory, "LookAway.exe"), TestContext.Current.CancellationToken));
        Assert.Empty(_process.WaitedFor);
        _ = Assert.Single(_process.AppsStarted);
        Assert.Equal(1, _process.ExitCount);
    }

    /// <remarks>Scheitert der Datei-Tausch, ist der vorige Stand zurückgerollt und startet wieder.</remarks>
    [Fact]
    public async Task Helper_WhenTheReplacementFails_RestartsTheRolledBackApp()
    {
        string sha = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.0.0", PendingUpdateSha256 = sha });
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(9, 0, 0));
        UpdateApplyArgs apply = new(FakeUpdateProcess.ProcessId, Path.Combine(_stagingRoot, "9.0.0"), _installDirectory);

        using (FileStream readOnly = new(Path.Combine(_installDirectory, "LookAway.exe"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await orchestrator.RunHelperApplyAsync(apply);
        }

        Assert.Equal("alte-exe", await File.ReadAllTextAsync(Path.Combine(_installDirectory, "LookAway.exe"), TestContext.Current.CancellationToken));
        _ = Assert.Single(_process.AppsStarted);
        Assert.Equal(1, _process.ExitCount);
    }

    [Fact]
    public async Task Helper_WhenTheAppCannotRestart_StillEnds()
    {
        string sha = StageVersion("9.0.0");
        InMemorySettingsRepository repository = new(new Settings { PendingUpdateVersion = "9.0.0", PendingUpdateSha256 = sha });
        _process.StartAppFailure = new Win32Exception("blockiert");
        UpdateOrchestrator orchestrator = CreateOrchestrator(repository, new Version(9, 0, 0));
        UpdateApplyArgs apply = new(FakeUpdateProcess.ProcessId, Path.Combine(_stagingRoot, "9.0.0"), _installDirectory);

        await orchestrator.RunHelperApplyAsync(apply);

        Assert.Empty(_process.AppsStarted);
        Assert.Equal(1, _process.ExitCount);
    }
}
