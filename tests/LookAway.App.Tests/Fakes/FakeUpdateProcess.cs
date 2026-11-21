using LookAway.App.Services;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Hält fest, welche Prozessaktionen der Update-Ablauf auslöst, statt sie auszuführen. Jede
/// Startaktion lässt sich auf einen Fehler stellen.
/// </summary>
internal sealed class FakeUpdateProcess : IUpdateProcess
{
    public const int ProcessId = 4711;

    public FakeUpdateProcess(string installDirectory) => InstallDirectory = installDirectory;

    public string InstallDirectory { get; }

    public int CurrentProcessId => ProcessId;

    public List<(string ExePath, UpdateApplyArgs Args)> AppliesStarted { get; } = [];

    public List<string> AppsStarted { get; } = [];

    public List<Uri> BrowserOpened { get; } = [];

    public List<int> WaitedFor { get; } = [];

    public int ExitCount { get; private set; }

    public bool IsSetupInstallation { get; set; }

    public List<string> SetupsStarted { get; } = [];

    public Exception? StartSetupFailure { get; set; }

    /// <summary>Läuft beim Start eines Setups, bevor es als gestartet gilt.</summary>
    public Action? SetupStarting { get; set; }

    public void StartSetup(string setupPath)
    {
        SetupStarting?.Invoke();
        if (StartSetupFailure is not null)
        {
            throw StartSetupFailure;
        }

        SetupsStarted.Add(setupPath);
    }

    public Exception? StartApplyFailure { get; set; }

    public Exception? StartAppFailure { get; set; }

    public Exception? BrowserFailure { get; set; }

    public void StartApply(string stagedExePath, UpdateApplyArgs args)
    {
        if (StartApplyFailure is not null)
        {
            throw StartApplyFailure;
        }

        AppliesStarted.Add((stagedExePath, args));
    }

    public void StartApp(string exePath)
    {
        if (StartAppFailure is not null)
        {
            throw StartAppFailure;
        }

        AppsStarted.Add(exePath);
    }

    public void OpenInBrowser(Uri address)
    {
        if (BrowserFailure is not null)
        {
            throw BrowserFailure;
        }

        BrowserOpened.Add(address);
    }

    public void WaitForExit(int processId, TimeSpan timeout) => WaitedFor.Add(processId);

    public void Exit() => ExitCount++;
}
