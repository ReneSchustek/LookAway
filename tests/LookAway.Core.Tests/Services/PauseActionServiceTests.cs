using LookAway.Core.Interfaces;
using LookAway.Core.Services;
using LookAway.Core.Tests.Fakes;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für die UI-freie Koordination der Pause-Aktionen.
/// </summary>
public sealed class PauseActionServiceTests
{
    private static readonly string[] ExpectedDimThenRestore = { "Dim", "Restore" };

    private static PauseActionService Create(out FakeScreenDimmer dimmer, out FakeMediaController media)
    {
        dimmer = new FakeScreenDimmer();
        media = new FakeMediaController();
        return new PauseActionService(dimmer, media);
    }

    [Fact]
    public async Task BeginBreak_dimmt_und_pausiert_wenn_aktiviert()
    {
        using PauseActionService service = Create(out FakeScreenDimmer dimmer, out FakeMediaController media);
        service.DimScreenEnabled = true;
        service.DimBrightnessPercent = 25;
        service.PauseMediaEnabled = true;

        await service.BeginBreakAsync();

        Assert.Equal(1, dimmer.DimCallCount);
        Assert.Equal(25, dimmer.LastTargetPercent);
        Assert.Equal(1, media.PauseCallCount);
    }

    [Fact]
    public async Task BeginBreak_tut_nichts_wenn_deaktiviert()
    {
        using PauseActionService service = Create(out FakeScreenDimmer dimmer, out FakeMediaController media);

        await service.BeginBreakAsync();

        Assert.Equal(0, dimmer.DimCallCount);
        Assert.Equal(0, media.PauseCallCount);
    }

    [Fact]
    public async Task EndBreak_stellt_wieder_her_und_setzt_Medien_fort()
    {
        using PauseActionService service = Create(out FakeScreenDimmer dimmer, out FakeMediaController media);
        service.DimScreenEnabled = true;
        service.PauseMediaEnabled = true;
        service.ResumeMediaAfterBreak = true;

        await service.EndBreakAsync();

        Assert.Equal(1, dimmer.RestoreCallCount);
        Assert.Equal(1, media.ResumeCallCount);
    }

    [Fact]
    public async Task EndBreak_setzt_Medien_nicht_fort_wenn_abgewählt()
    {
        using PauseActionService service = Create(out _, out FakeMediaController media);
        service.PauseMediaEnabled = true;
        service.ResumeMediaAfterBreak = false;

        await service.EndBreakAsync();

        Assert.Equal(0, media.ResumeCallCount);
    }

    [Fact]
    public async Task Begin_und_End_laufen_serialisiert_Restore_nach_DimTo()
    {
        OrderRecordingDimmer dimmer = new();
        using PauseActionService service = new(dimmer, new FakeMediaController())
        {
            DimScreenEnabled = true,
        };

        // Begin und End gleichzeitig anstoßen (wie im Coordinator „fire-and-forget").
        Task begin = service.BeginBreakAsync();
        Task end = service.EndBreakAsync();
        await Task.WhenAll(begin, end);

        // Das Semaphore erzwingt die Aufrufreihenfolge: Dimmen vor Wiederherstellen.
        Assert.Equal(ExpectedDimThenRestore, dimmer.Calls);
    }

    // Zeichnet die Reihenfolge der Dim/Restore-Aufrufe auf.
    private sealed class OrderRecordingDimmer : IScreenDimmer
    {
        private readonly List<string> _calls = new();
        private readonly Lock _gate = new();

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (_gate)
                {
                    return _calls.ToArray();
                }
            }
        }

        public void DimTo(int targetPercent)
        {
            lock (_gate)
            {
                _calls.Add("Dim");
            }
        }

        public void Restore()
        {
            lock (_gate)
            {
                _calls.Add("Restore");
            }
        }
    }
}
