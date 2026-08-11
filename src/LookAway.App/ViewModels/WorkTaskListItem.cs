using CommunityToolkit.Mvvm.ComponentModel;

namespace LookAway.App.ViewModels;

/// <summary>
/// Eine Aufgabe als Kachel in der Liste.
/// </summary>
/// <remarks>
/// Der Bearbeitungszustand liegt am Eintrag und nicht an der Liste: So lässt sich eine
/// Aufgabe umbenennen, während die anderen normal dastehen.
/// </remarks>
internal sealed partial class WorkTaskListItem : ObservableObject
{
    /// <summary>Erzeugt den Eintrag.</summary>
    /// <param name="id">Kennung der Aufgabe.</param>
    /// <param name="text">Beschreibung.</param>
    /// <param name="isCompleted">Wahr, wenn die Aufgabe erledigt ist.</param>
    /// <param name="createdAtText">Zeitpunkt der Anlage, fertig formatiert.</param>
    /// <param name="breakCount">Anzahl der Pausen, die an dieser Aufgabe hingen.</param>
    /// <param name="breakCountText">Die Anzahl als lokalisierter Satz.</param>
    public WorkTaskListItem(
        Guid id,
        string text,
        bool isCompleted,
        string createdAtText,
        int breakCount,
        string breakCountText)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(createdAtText);
        ArgumentNullException.ThrowIfNull(breakCountText);
        ArgumentOutOfRangeException.ThrowIfNegative(breakCount);

        Id = id;
        CreatedAtText = createdAtText;
        BreakCount = breakCount;
        BreakCountText = breakCountText;
        Text = text;
        EditText = text;
        IsCompleted = isCompleted;
    }

    /// <summary>Kennung der Aufgabe.</summary>
    public Guid Id { get; }

    /// <summary>Zeitpunkt der Anlage, fertig formatiert.</summary>
    public string CreatedAtText { get; }

    /// <summary>Anzahl der Pausen, die an dieser Aufgabe hingen.</summary>
    public int BreakCount { get; }

    /// <summary>Die Anzahl als lokalisierter Satz.</summary>
    public string BreakCountText { get; }

    /// <summary>Beschreibung der Aufgabe.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>Der Text während einer Bearbeitung; erst das Übernehmen schreibt ihn zurück.</summary>
    [ObservableProperty]
    public partial string EditText { get; set; }

    /// <summary>Wahr, wenn die Aufgabe erledigt ist.</summary>
    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    /// <summary>Wahr, solange die Aufgabe umbenannt wird.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; set; }
}
