namespace LookAway.App.ViewModels;

/// <summary>
/// Filter auf die Stufe eines Protokolleintrags.
/// </summary>
/// <remarks>
/// <see cref="Information"/> schließt die selteneren Stufen darunter (Debug, Trace)
/// mit ein, <see cref="Error"/> die schwerere darüber (Critical). Ein Critical, das
/// hinter einem Filter „Fehler" verschwindet, wäre genau die Meldung, die niemand
/// finden würde.
/// </remarks>
internal enum LogLevelFilter
{
    /// <summary>Alle Stufen.</summary>
    All,

    /// <summary>Hinweise und darunter.</summary>
    Information,

    /// <summary>Nur Warnungen.</summary>
    Warning,

    /// <summary>Fehler und schwerwiegende Fehler.</summary>
    Error,
}

/// <summary>
/// Filter auf den Zeitraum eines Protokolleintrags.
/// </summary>
internal enum LogPeriodFilter
{
    /// <summary>Alles, was aufbewahrt wird.</summary>
    All,

    /// <summary>Nur der heutige Tag.</summary>
    Today,

    /// <summary>Die letzten sieben Tage.</summary>
    Week,
}
