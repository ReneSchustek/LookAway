using LookAway.Core.Domain;

namespace LookAway.Tests.Unit.Core;

/// <summary>
/// Tests für <see cref="SessionMarker"/>: ein Neustart innerhalb derselben
/// Windows-Sitzung wird als „gleiche Sitzung" erkannt, ein System-Neustart nicht.
/// </summary>
public sealed class SessionMarkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Compute_LiefertSystemstartZeitpunkt()
    {
        // 1000 ms seit Systemstart -> Marke liegt 1 Sekunde vor "jetzt".
        Assert.Equal(Now - TimeSpan.FromSeconds(1), SessionMarker.Compute(Now, 1000));
    }

    [Fact]
    public void IsSameSession_BeiNeustartInDerselbenSitzung_IstWahr()
    {
        // 30 s später, Laufzeit ebenfalls +30 s -> identische Marke (gleiche Sitzung).
        DateTimeOffset beforeRestart = SessionMarker.Compute(Now, 60_000);
        DateTimeOffset afterRestart = SessionMarker.Compute(Now + TimeSpan.FromSeconds(30), 90_000);

        Assert.True(SessionMarker.IsSameSession(beforeRestart, afterRestart));
    }

    [Fact]
    public void IsSameSession_NachWindowsNeustart_IstFalsch()
    {
        // Vor dem Neustart 1 h Laufzeit; nach dem Neustart nur wenige Sekunden Laufzeit
        // bei deutlich späterer Uhrzeit -> stark verschobene Marke.
        DateTimeOffset beforeReboot = SessionMarker.Compute(Now, 3_600_000);
        DateTimeOffset afterReboot = SessionMarker.Compute(Now + TimeSpan.FromHours(2), 5_000);

        Assert.False(SessionMarker.IsSameSession(beforeReboot, afterReboot));
    }
}
