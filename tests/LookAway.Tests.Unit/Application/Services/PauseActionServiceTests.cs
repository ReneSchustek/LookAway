using LookAway.Application.Services;
using LookAway.Tests.Unit.Fakes;

namespace LookAway.Tests.Unit.Application.Services;

/// <summary>
/// Tests fuer die UI-freie Koordination der Pause-Aktionen.
/// </summary>
public sealed class PauseActionServiceTests
{
    private static PauseActionService Create(out FakeScreenDimmer dimmer, out FakeMediaController media)
    {
        dimmer = new FakeScreenDimmer();
        media = new FakeMediaController();
        return new PauseActionService(dimmer, media);
    }

    [Fact]
    public async Task BeginBreak_dimmt_und_pausiert_wenn_aktiviert()
    {
        PauseActionService service = Create(out FakeScreenDimmer dimmer, out FakeMediaController media);
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
        PauseActionService service = Create(out FakeScreenDimmer dimmer, out FakeMediaController media);

        await service.BeginBreakAsync();

        Assert.Equal(0, dimmer.DimCallCount);
        Assert.Equal(0, media.PauseCallCount);
    }

    [Fact]
    public async Task EndBreak_stellt_wieder_her_und_setzt_Medien_fort()
    {
        PauseActionService service = Create(out FakeScreenDimmer dimmer, out FakeMediaController media);
        service.DimScreenEnabled = true;
        service.PauseMediaEnabled = true;
        service.ResumeMediaAfterBreak = true;

        await service.EndBreakAsync();

        Assert.Equal(1, dimmer.RestoreCallCount);
        Assert.Equal(1, media.ResumeCallCount);
    }

    [Fact]
    public async Task EndBreak_setzt_Medien_nicht_fort_wenn_abgewaehlt()
    {
        PauseActionService service = Create(out _, out FakeMediaController media);
        service.PauseMediaEnabled = true;
        service.ResumeMediaAfterBreak = false;

        await service.EndBreakAsync();

        Assert.Equal(0, media.ResumeCallCount);
    }
}
