using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace LookAway.MeasurementData;

/// <summary>
/// Eine Woche Protokolldateien im Schreibformat von LookAway, aus einem festen Startwert erzeugt.
/// </summary>
/// <remarks>
/// So lange bewahrt LookAway das Protokoll auf. Die Mischung entspricht dem, was das Programm
/// schreibt: meist Hinweise, ein Teil Debug, wenige Warnungen und Fehler; ein Fehler trägt einen
/// Ausnahme-Stapel über mehrere Zeilen, wie ihn die Protokollseite zusammensetzen muss.
/// </remarks>
[SuppressMessage("Security", "CA5394", Justification = "Fester Startwert für einen reproduzierbaren Messbestand; die Zahlen haben keinen Sicherheitsbezug.")]
internal static class LogWeek
{
    /// <summary>Startwert des Zufallsgenerators.</summary>
    public const int Seed = 20261122;

    /// <summary>Aufbewahrungsdauer des Protokolls in Tagen.</summary>
    public const int Days = 7;

    private static readonly string[] Categories =
    [
        "LookAway.Core.Services.TimerService",
        "LookAway.Core.Services.BreakCoordinator",
        "LookAway.Core.Services.IdleDetectionService",
        "LookAway.Data.Repositories.JsonSettingsRepository",
        "LookAway.Data.Repositories.JsonBreakHistoryRepository",
        "LookAway.App.Services.TrayIconService",
        "LookAway.App.Services.BreakOverlayPresenter",
    ];

    private static readonly string[] Messages =
    [
        "Timer gestartet: Arbeit 00:25:00, Pause 00:05:00.",
        "Pause fällig: Erinnerung wird gezeigt.",
        "Timer nach wiederkehrender Aktivität fortgesetzt.",
        "Einstellungen in %APPDATA%\\LookAway\\settings.json gespeichert.",
        "Pausen-Sitzung an %APPDATA%\\LookAway\\history.json angehängt.",
        "Tray-Icon eingeblendet.",
        "Vollbild-Anwendung erkannt; Erinnerung zurückgestellt.",
    ];

    /// <summary>
    /// Schreibt sieben Tagesdateien mit je <paramref name="linesPerDay"/> Einträgen, die jüngste
    /// für <paramref name="lastDay"/>.
    /// </summary>
    /// <param name="directory">Zielordner (wird angelegt).</param>
    /// <param name="lastDay">Tag der jüngsten Datei.</param>
    /// <param name="linesPerDay">Einträge je Tag.</param>
    /// <returns>Gesamtzahl der geschriebenen Einträge.</returns>
    public static int WriteTo(string directory, DateOnly lastDay, int linesPerDay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(linesPerDay);
        _ = Directory.CreateDirectory(directory);
        Random random = new(Seed);

        for (int offset = 0; offset < Days; offset++)
        {
            DateOnly day = lastDay.AddDays(-offset);
            string path = Path.Combine(directory, $"lookaway-{day:yyyy-MM-dd}.log");
            File.WriteAllText(path, Day(random, day, linesPerDay), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        return Days * linesPerDay;
    }

    private static string Day(Random random, DateOnly day, int lines)
    {
        StringBuilder text = new();
        DateTime start = day.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
        double step = TimeSpan.FromHours(14).TotalMilliseconds / lines;

        for (int line = 0; line < lines; line++)
        {
            DateTime time = start.AddMilliseconds(line * step);
            int roll = random.Next(100);
            string level = roll < 70 ? "Information" : roll < 90 ? "Debug" : roll < 98 ? "Warning" : "Error";
            _ = text.Append(CultureInfo.InvariantCulture, $"[{time:yyyy-MM-ddTHH:mm:ss.fffZ}] [{level}] [p{4711 + (line / 500)}] ")
                .Append(Categories[random.Next(Categories.Length)])
                .Append(": ")
                .AppendLine(Messages[random.Next(Messages.Length)]);

            if (level == "Error")
            {
                AppendStack(text);
            }
        }

        return text.ToString();
    }

    private static void AppendStack(StringBuilder text)
    {
        _ = text.AppendLine("System.IO.IOException: Der Prozess kann nicht auf die Datei zugreifen, da sie von einem anderen Prozess verwendet wird.");
        for (int frame = 0; frame < 10; frame++)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"   at LookAway.Data.JsonFileStore.WriteAsync(String content, CancellationToken token) in JsonFileStore.cs:line {100 + frame}").AppendLine();
        }
    }
}
