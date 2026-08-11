namespace LookAway.App.ViewModels;

/// <summary>
/// Die zwei Fragen, die jede Listen-Ansicht gleich beantwortet: Ist etwas zu sehen,
/// und wenn nicht — liegt das an fehlenden Daten oder an der Eingabe?
/// </summary>
/// <param name="Total">Anzahl aller vorhandenen Einträge, ungefiltert.</param>
/// <param name="Visible">Anzahl der nach Suche und Filter sichtbaren Einträge.</param>
/// <remarks>
/// Als eigener Wert und nicht als Basisklasse: Die drei Listen der Anwendung führen
/// verschiedene Filtertypen und Datenquellen: Vererbung würde sie aneinander binden,
/// ohne dass sie etwas voneinander bräuchten. Sie halten stattdessen einen dieser
/// Werte und beantworten ihre Fragen daraus.
/// </remarks>
internal readonly record struct ListViewState(int Total, int Visible)
{
    /// <summary>Wahr, solange Einträge zu sehen sind.</summary>
    public bool HasResults => Visible > 0;

    /// <summary>
    /// Wahr, wenn überhaupt nichts vorhanden ist.
    /// </summary>
    /// <remarks>
    /// Von <see cref="ShowNoResults"/> zu trennen: Eine leere Datenlage ist eine andere
    /// Lage als eine Suche ohne Treffer, und ein „Suche zurücksetzen" hilft dort nicht.
    /// </remarks>
    public bool ShowEmpty => Total == 0;

    /// <summary>Wahr, wenn Suche oder Filter alles ausgeblendet haben.</summary>
    public bool ShowNoResults => Total > 0 && Visible == 0;
}
