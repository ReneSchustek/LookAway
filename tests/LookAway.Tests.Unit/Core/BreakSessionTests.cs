using LookAway.Core.Entities;
using LookAway.Core.Enums;

namespace LookAway.Tests.Unit.Core;

/// <summary>
/// Tests fuer die Invarianten der <see cref="BreakSession"/>-Entitaet.
/// </summary>
public sealed class BreakSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Konstruktor_setzt_Werte_und_berechnet_die_Dauer()
    {
        BreakSession session = new(
            Guid.NewGuid(),
            Start,
            Start.AddMinutes(5),
            BreakModel.ClassicPomodoro,
            BreakOutcome.Taken);

        Assert.Equal(TimeSpan.FromMinutes(5), session.Duration);
        Assert.Equal(BreakOutcome.Taken, session.Outcome);
    }

    [Fact]
    public void Leere_Id_wird_abgelehnt()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new BreakSession(Guid.Empty, Start, Start, BreakModel.ClassicPomodoro, BreakOutcome.Taken));
    }

    [Fact]
    public void Ende_vor_Beginn_wird_abgelehnt()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BreakSession(Guid.NewGuid(), Start, Start.AddMinutes(-1), BreakModel.ClassicPomodoro, BreakOutcome.Taken));
    }
}
