using System.Globalization;
using System.Text;
using LookAway.Core.Entities;

namespace LookAway.Application.Statistics;

/// <summary>
/// Erzeugt aus Pausen-Sitzungen eine CSV-Darstellung (BRIEF018). Zeitstempel und
/// Dauer werden invariant (maschinenlesbar) formatiert; Felder werden bei Bedarf
/// CSV-konform escaped.
/// </summary>
public sealed class CsvExporter
{
    private const string Header = "StartedAt,EndedAt,Duration,Model,Outcome";
    private const string LineEnding = "\r\n";

    /// <summary>
    /// Exportiert die Sitzungen als CSV-Text (Komma-getrennt, eine Kopfzeile).
    /// </summary>
    /// <param name="sessions">Zu exportierende Sitzungen.</param>
    /// <returns>Der CSV-Inhalt als Zeichenkette.</returns>
    public string Export(IEnumerable<BreakSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        StringBuilder builder = new();
        _ = builder.Append(Header).Append(LineEnding);

        foreach (BreakSession session in sessions)
        {
            _ = builder
                .Append(Escape(session.StartedAt.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Escape(session.EndedAt.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Escape(session.Duration.ToString("c", CultureInfo.InvariantCulture))).Append(',')
                .Append(Escape(session.Model.ToString())).Append(',')
                .Append(Escape(session.Outcome.ToString()))
                .Append(LineEnding);
        }

        return builder.ToString();
    }

    private static string Escape(string field)
    {
        if (field.Contains(',', StringComparison.Ordinal)
            || field.Contains('"', StringComparison.Ordinal)
            || field.Contains('\n', StringComparison.Ordinal)
            || field.Contains('\r', StringComparison.Ordinal))
        {
            return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return field;
    }
}
