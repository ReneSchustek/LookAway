using LookAway.App.Services;

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
    [InlineData("ftp://example.org/datei")]
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
}
