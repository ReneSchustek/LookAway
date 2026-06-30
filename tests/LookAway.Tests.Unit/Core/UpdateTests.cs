using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Tests.Unit.Core;

/// <summary>
/// Tests für die Update-Vergleichslogik und die Prüf-Fälligkeit.
/// </summary>
public sealed class UpdateTests
{
    private static readonly Version Current = new(1, 2, 0);

    [Fact]
    public void Create_erkennt_neuere_Version()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.3.0", "https://example.com/r", "Notes");

        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("1.3.0", info.LatestVersion);
        Assert.NotNull(info.DownloadUrl);
    }

    [Fact]
    public void Create_meldet_kein_Update_bei_gleicher_Version()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.2.0", "https://example.com/r", "Notes");

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public void Create_übernimmt_Paket_URL()
    {
        UpdateInfo info = UpdateInfo.Create(
            Current,
            "v1.3.0",
            "https://example.com/r",
            "Notes",
            "https://example.com/LookAway-Portable-v1.3.0.zip");

        Assert.NotNull(info.PackageUrl);
        Assert.Equal("https://example.com/LookAway-Portable-v1.3.0.zip", info.PackageUrl!.ToString());
    }

    [Fact]
    public void Create_ohne_Paket_URL_hat_keine()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.3.0", "https://example.com/r", "Notes");

        Assert.Null(info.PackageUrl);
    }

    [Fact]
    public void Create_meldet_kein_Update_bei_älterer_Version()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "1.1.9", null, null);

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public void Create_ignoriert_unparsbares_Tag()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "nightly", null, null);

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public void Create_schneidet_Prärelease_Suffix_ab()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.3.0-beta", null, null);

        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("1.3.0", info.LatestVersion);
    }

    [Theory]
    [InlineData(UpdateCheckFrequency.OnStartup, 0, true)]
    [InlineData(UpdateCheckFrequency.Daily, 12, false)]
    [InlineData(UpdateCheckFrequency.Daily, 25, true)]
    [InlineData(UpdateCheckFrequency.Weekly, 100, false)]
    [InlineData(UpdateCheckFrequency.Weekly, 200, true)]
    public void IsDue_beachtet_Häufigkeit(UpdateCheckFrequency frequency, int hoursSinceLast, bool expected)
    {
        DateTimeOffset now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset last = now.AddHours(-hoursSinceLast);

        Assert.Equal(expected, UpdateSchedule.IsDue(frequency, last, now));
    }

    [Fact]
    public void IsDue_ist_wahr_wenn_nie_geprüft()
    {
        DateTimeOffset now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Weekly, lastCheck: null, now));
    }
}
