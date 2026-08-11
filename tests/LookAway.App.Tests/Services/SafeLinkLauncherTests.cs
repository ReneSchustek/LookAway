using LookAway.App.Services;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Services;

/// <summary>
/// Sichert den Starter ab: Er öffnet Webadressen und sonst nichts. Die
/// Dokumentations-Adresse stammt aus den Sprachdateien — ohne diese Prüfung
/// startete an dieser Stelle, was dort eingetragen ist.
/// </summary>
public sealed class SafeLinkLauncherTests
{
    [Theory]
    [InlineData("https://paypal.me/rschustek")]
    [InlineData("https://github.com/ReneSchustek/LookAway")]
    [InlineData("http://example.org/hilfe")]
    public void IsAllowed_AcceptsAbsoluteWebLinks(string link)
        => Assert.True(SafeLinkLauncher.IsAllowed(new Uri(link)));

    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ftp://example.org/file")]
    [InlineData("mailto:jemand@example.org")]
    [InlineData("ms-settings:privacy")]
    [InlineData("javascript:alert(1)")]
    public void IsAllowed_RejectsEverythingButHttpAndHttps(string link)
        => Assert.False(SafeLinkLauncher.IsAllowed(new Uri(link)));

    [Fact]
    public void IsAllowed_RejectsRelativeLinks()
        => Assert.False(SafeLinkLauncher.IsAllowed(new Uri("/hilfe", UriKind.Relative)));

    [Fact]
    public void IsAllowed_RejectsNull()
        => Assert.False(SafeLinkLauncher.IsAllowed(null));

    /// <remarks>
    /// Das Spendenziel muss die Prüfung bestehen — sonst bliebe die Schaltfläche
    /// zwar sichtbar, täte aber nichts.
    /// </remarks>
    [Fact]
    public void IsAllowed_AcceptsTheDonationTarget()
        => Assert.True(SafeLinkLauncher.IsAllowed(new Uri(SupportDonation.PayPalUrl)));

    /// <remarks>
    /// Der zweite Weg, auf dem eine fremde Adresse ins Programm kommt: die Antwort des
    /// Release-Dienstes. Die Auswertung nimmt jedes absolute Schema an — dieser Test
    /// hält fest, dass sie das tut, und dass erst die Prüfung beim Öffnen den
    /// Unterschied macht. Fiele sie weg, startete die Schaltfläche „Zur Release-Seite"
    /// nicht den Browser, sondern was das System für dieses Schema hinterlegt hat.
    /// </remarks>
    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ms-settings:privacy")]
    public void UpdateInfo_PassesForeignSchemesThrough_SoTheCheckIsNeeded(string address)
    {
        UpdateInfo info = UpdateInfo.Create(
            new Version(1, 0, 0),
            tagName: "v2.0.0",
            htmlAddress: address,
            releaseNotes: string.Empty);

        Assert.NotNull(info.DownloadUrl);
        Assert.False(SafeLinkLauncher.IsAllowed(info.DownloadUrl));
    }

    [Fact]
    public void UpdateInfo_WithAWebAddress_IsAllowed()
    {
        UpdateInfo info = UpdateInfo.Create(
            new Version(1, 0, 0),
            tagName: "v2.0.0",
            htmlAddress: "https://github.com/ReneSchustek/LookAway/releases/tag/v2.0.0",
            releaseNotes: string.Empty);

        Assert.True(SafeLinkLauncher.IsAllowed(info.DownloadUrl));
    }
}
