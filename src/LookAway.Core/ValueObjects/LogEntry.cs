using Microsoft.Extensions.Logging;

namespace LookAway.Core.ValueObjects;

/// <summary>
/// Eine gelesene Zeile des Anwendungsprotokolls.
/// </summary>
/// <param name="Timestamp">Zeitpunkt des Eintrags (UTC, wie geschrieben).</param>
/// <param name="Level">Stufe des Eintrags.</param>
/// <param name="Category">Herkunft — in der Regel der Typname des Schreibers.</param>
/// <param name="Message">Meldungstext, inklusive angehängter Folgezeilen.</param>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Category, string Message);
