using LookAway.Core.ValueObjects;

namespace LookAway.Core.Tests;

/// <summary>
/// Erzeugen und Lesen der Argumente des Update-Helfers. Beide Seiten müssen dasselbe Format
/// sprechen, und eine unvollständige Zeile darf nicht als Helfer-Aufruf gelten, sonst liefe
/// der Helfer ohne Quelle oder Ziel los.
/// </summary>
public sealed class UpdateApplyArgsTests
{
    [Fact]
    public void RoundTrips_ThroughArgumentList()
    {
        UpdateApplyArgs original = new(1234, @"C:\staging\1.5.0", @"C:\Program Files\LookAway");

        // Argumentliste wie beim Helfer-Start, plus Programmname an Index 0.
        List<string> args = new() { "LookAway.exe" };
        args.AddRange(original.ToArgumentList());

        Assert.True(UpdateApplyArgs.TryParse(args, out UpdateApplyArgs parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void TryParse_IgnoresArgumentOrder()
    {
        string[] args =
        [
            "LookAway.exe", "--target", @"C:\app", "--source", @"C:\src", "--pid", "42", UpdateApplyArgs.ApplyFlag,
        ];

        Assert.True(UpdateApplyArgs.TryParse(args, out UpdateApplyArgs parsed));
        Assert.Equal(42, parsed.Pid);
        Assert.Equal(@"C:\src", parsed.Source);
        Assert.Equal(@"C:\app", parsed.Target);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenFlagMissing()
    {
        string[] args = ["LookAway.exe", "--pid", "42", "--source", "s", "--target", "t"];
        Assert.False(UpdateApplyArgs.TryParse(args, out _));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void TryParse_ReturnsFalse_WhenPidInvalid(string pid)
    {
        string[] args = [UpdateApplyArgs.ApplyFlag, "--pid", pid, "--source", "s", "--target", "t"];
        Assert.False(UpdateApplyArgs.TryParse(args, out _));
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenSourceOrTargetMissing()
    {
        string[] args = [UpdateApplyArgs.ApplyFlag, "--pid", "42", "--source", "s"];
        Assert.False(UpdateApplyArgs.TryParse(args, out _));
    }

    [Fact]
    public void TryParse_HandlesNull() => Assert.False(UpdateApplyArgs.TryParse(null, out _));
}
