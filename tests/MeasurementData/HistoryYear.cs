using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using LookAway.Core.Entities;
using LookAway.Core.Enums;

namespace LookAway.MeasurementData;

/// <summary>
/// Ein Jahr Pausen-Historie eines häufig pausierenden Nutzers, aus einem festen Startwert erzeugt —
/// auf jedem Rechner derselbe Bestand.
/// </summary>
/// <remarks>
/// So lange bewahrt LookAway die Historie auf. An Werktagen 10 bis 24 Pausen (kurze Pausen alle 20
/// Minuten ergeben die obere Grenze), am Wochenende höchstens drei; Uhrzeiten über den Arbeitstag
/// verteilt. Ergibt rund 4.500 Einträge — mehr, als ein durchschnittlicher Nutzer sammelt, aber
/// keine runde Million, die niemand erreicht.
/// </remarks>
[SuppressMessage("Security", "CA5394", Justification = "Fester Startwert für einen reproduzierbaren Messbestand; die Zahlen haben keinen Sicherheitsbezug.")]
internal static class HistoryYear
{
    /// <summary>Startwert des Zufallsgenerators; derselbe Wert ergibt denselben Bestand.</summary>
    public const int Seed = 20261121;

    private static readonly BreakModel[] Models = Enum.GetValues<BreakModel>();

    // Wie die Ablage schreibt: camelCase, Aufzählungen als Text, eingerückt. Ob das Format passt,
    // prüft jeder Lauf, indem er den Bestand über die echte Ablage wieder einliest.
    private static readonly JsonSerializerOptions FileFormat = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Erzeugt die Pausen des Jahres vor <paramref name="now"/>.</summary>
    /// <param name="now">Zeitpunkt, an dem das Jahr endet.</param>
    /// <returns>Die Pausen in zeitlicher Reihenfolge.</returns>
    public static IReadOnlyList<BreakSession> Create(DateTimeOffset now)
    {
        Random random = new(Seed);
        Guid[] tasks = Enumerable.Range(0, 8).Select(_ => NewGuid(random)).ToArray();
        List<BreakSession> sessions = [];

        for (DateTimeOffset day = now.Date.AddDays(-364); day <= now; day = day.AddDays(1))
        {
            bool weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            int count = weekend ? random.Next(0, 4) : random.Next(10, 25);
            foreach (int minute in DayMinutes(random, count))
            {
                sessions.Add(CreateSession(random, day.AddMinutes(minute), tasks));
            }
        }

        return sessions;
    }

    /// <summary>Schreibt die Pausen im Format der Historie-Datei.</summary>
    /// <param name="path">Zieldatei.</param>
    /// <param name="sessions">Die Pausen.</param>
    public static void WriteTo(string path, IEnumerable<BreakSession> sessions)
        => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(sessions.ToList(), FileFormat));

    // Uhrzeiten zwischen 8 und 18 Uhr, aufsteigend.
    private static IEnumerable<int> DayMinutes(Random random, int count)
        => Enumerable.Range(0, count).Select(_ => random.Next(8 * 60, 18 * 60)).Order();

    private static BreakSession CreateSession(Random random, DateTimeOffset start, Guid[] tasks)
    {
        int roll = random.Next(100);
        BreakOutcome outcome = roll < 70 ? BreakOutcome.Taken : roll < 85 ? BreakOutcome.Skipped : BreakOutcome.Snoozed;
        TimeSpan length = outcome == BreakOutcome.Taken ? TimeSpan.FromSeconds(random.Next(20, 1200)) : TimeSpan.Zero;
        Guid? task = random.Next(100) < 30 ? tasks[random.Next(tasks.Length)] : null;

        return new BreakSession(NewGuid(random), start, start + length, Models[random.Next(Models.Length)], outcome, task);
    }

    // Aus dem Zufallsgenerator statt Guid.NewGuid: sonst wiche der Bestand von Lauf zu Lauf ab.
    private static Guid NewGuid(Random random)
    {
        byte[] bytes = new byte[16];
        random.NextBytes(bytes);
        return new Guid(bytes);
    }
}
