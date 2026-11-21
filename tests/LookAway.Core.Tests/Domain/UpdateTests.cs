using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Tests;

/// <summary>
/// Versionsvergleich und Prüffälligkeit der Aktualisierung. Ein falscher Vergleich böte eine
/// ältere oder dieselbe Version als Update an oder verpasste eine neuere; eine falsche
/// Fälligkeit fragte bei jedem Start oder nie.
/// </summary>
public sealed class UpdateTests
{
    private static readonly Version Current = new(1, 2, 0);

    [Fact]
    public void Create_DetectsANewerVersion()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.3.0", "https://example.com/r", "Notes");

        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("1.3.0", info.LatestVersion);
        Assert.NotNull(info.DownloadUrl);
    }

    [Fact]
    public void Create_WithTheSameVersion_ReportsNoUpdate()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.2.0", "https://example.com/r", "Notes");

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public void Create_TakesSetupAndSignatureAddresses()
    {
        UpdateInfo info = UpdateInfo.Create(
            Current,
            "v1.3.0",
            null,
            null,
            new ReleaseAssets(
                "https://example.com/p.zip",
                "https://example.com/p.zip.sig",
                "https://example.com/LookAway-Setup-v1.3.0.exe",
                "https://example.com/LookAway-Setup-v1.3.0.exe.sig"));

        Assert.Equal("https://example.com/p.zip.sig", info.SignatureUrl?.ToString());
        Assert.Equal("https://example.com/LookAway-Setup-v1.3.0.exe", info.SetupUrl?.ToString());
        Assert.Equal("https://example.com/LookAway-Setup-v1.3.0.exe.sig", info.SetupSignatureUrl?.ToString());
    }

    [Fact]
    public void Create_WithoutAssets_HasNoDownloads()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.3.0", null, null);

        Assert.Null(info.PackageUrl);
        Assert.Null(info.SetupUrl);
        Assert.Null(info.SetupSignatureUrl);
    }

    [Fact]
    public void Create_TakesThePackageAddress()
    {
        UpdateInfo info = UpdateInfo.Create(
            Current,
            "v1.3.0",
            "https://example.com/r",
            "Notes",
            new ReleaseAssets("https://example.com/LookAway-Portable-v1.3.0.zip", null));

        Assert.NotNull(info.PackageUrl);
        Assert.Equal("https://example.com/LookAway-Portable-v1.3.0.zip", info.PackageUrl!.ToString());
    }

    [Fact]
    public void Create_WithoutPackageAddress_HasNone()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "v1.3.0", "https://example.com/r", "Notes");

        Assert.Null(info.PackageUrl);
    }

    [Fact]
    public void Create_WithAnOlderVersion_ReportsNoUpdate()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "1.1.9", null, null);

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public void Create_IgnoresAnUnparsableTag()
    {
        UpdateInfo info = UpdateInfo.Create(Current, "nightly", null, null);

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public void Create_TrimsThePrereleaseSuffix()
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
    public void IsDue_RespectsTheFrequency(UpdateCheckFrequency frequency, int hoursSinceLast, bool expected)
    {
        DateTimeOffset now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset last = now.AddHours(-hoursSinceLast);

        Assert.Equal(expected, UpdateSchedule.IsDue(frequency, last, now));
    }

    [Fact]
    public void IsDue_WhenNeverChecked_IsTrue()
    {
        DateTimeOffset now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

        Assert.True(UpdateSchedule.IsDue(UpdateCheckFrequency.Weekly, lastCheck: null, now));
    }
}
