using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Beschriftungen der Aufgabenliste in der aktuellen Sprache.
/// </summary>
internal sealed partial class WorkTaskListViewModel
{
    /// <summary>Erklärende Zeile unter dem Titel.</summary>
    public string Subtitle => _localization.GetText(SettingsTextKeys.TasksSubtitle);

    /// <summary>Platzhalter des Suchfelds; er benennt, was durchsucht wird.</summary>
    public string SearchPlaceholder => _localization.GetText(SettingsTextKeys.TasksSearchPlaceholder);

    /// <summary>Platzhalter des Eingabefelds für eine neue Aufgabe.</summary>
    public string NewTaskPlaceholder => _localization.GetText(SettingsTextKeys.TasksNewPlaceholder);

    /// <summary>Beschriftung der Schaltfläche zum Anlegen.</summary>
    public string AddLabel => _localization.GetText(SettingsTextKeys.TasksAdd);

    /// <summary>Beschriftung des Filter-Chips "Alle".</summary>
    public string FilterAllLabel => _localization.GetText(SettingsTextKeys.TasksFilterAll);

    /// <summary>Beschriftung des Filter-Chips "Offen".</summary>
    public string FilterOpenLabel => _localization.GetText(SettingsTextKeys.TasksFilterOpen);

    /// <summary>Beschriftung des Filter-Chips "Erledigt".</summary>
    public string FilterCompletedLabel => _localization.GetText(SettingsTextKeys.TasksFilterCompleted);

    /// <summary>Beschriftung "Umbenennen".</summary>
    public string RenameLabel => _localization.GetText(SettingsTextKeys.TasksRename);

    /// <summary>Beschriftung "Löschen".</summary>
    public string DeleteLabel => _localization.GetText(SettingsTextKeys.TasksDelete);

    /// <summary>Beschriftung "Übernehmen".</summary>
    public string CommitLabel => _localization.GetText(SettingsTextKeys.TasksCommit);

    /// <summary>Beschriftung "Abbrechen".</summary>
    public string CancelLabel => _localization.GetText(SettingsTextKeys.TasksCancel);

    /// <summary>Frage vor dem Löschen.</summary>
    public string DeleteConfirmation => _localization.GetText(SettingsTextKeys.TasksDeleteConfirm);

    /// <summary>Überschrift des Leerzustands "noch keine Aufgabe".</summary>
    public string EmptyTitle => _localization.GetText(SettingsTextKeys.TasksEmptyTitle);

    /// <summary>Erklärung des Leerzustands "noch keine Aufgabe".</summary>
    public string EmptyText => _localization.GetText(SettingsTextKeys.TasksEmptyText);

    /// <summary>Überschrift des Leerzustands "nichts gefunden".</summary>
    public string NoResultsTitle => _localization.GetText(SettingsTextKeys.NoResultsTitle);

    /// <summary>Erklärung des Leerzustands "nichts gefunden".</summary>
    public string NoResultsText => _localization.GetText(SettingsTextKeys.NoResultsText);

    /// <summary>Beschriftung der Schaltfläche "Suche zurücksetzen".</summary>
    public string ResetSearchLabel => _localization.GetText(SettingsTextKeys.ResetSearch);
}
