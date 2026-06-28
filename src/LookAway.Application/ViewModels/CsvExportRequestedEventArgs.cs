namespace LookAway.Application.ViewModels;

/// <summary>
/// Traegt den fertigen CSV-Inhalt, der von der UI in eine Datei geschrieben
/// werden soll (BRIEF018).
/// </summary>
public sealed class CsvExportRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Erzeugt die Ereignisdaten.
    /// </summary>
    /// <param name="content">Der CSV-Inhalt.</param>
    public CsvExportRequestedEventArgs(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    /// <summary>Der zu speichernde CSV-Inhalt.</summary>
    public string Content { get; }
}
