using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Die Aufgaben des aufgabenbasierten Pausenmodells als durchsuchbare, filterbare
/// Liste — UI-frei und damit ohne WinUI testbar.
/// </summary>
/// <remarks>
/// Anlegen, Umbenennen, Abhaken und Löschen geschehen an der Kachel selbst und nicht
/// in einer eigenen Verwaltungsansicht: Wer zum Ändern erst woandershin wechseln muss,
/// verliert genau den Zusammenhang, den er vor sich hatte.
/// </remarks>
internal sealed partial class WorkTaskListViewModel : ObservableObject
{
    private readonly IWorkTaskRepository _repository;
    private readonly IBreakHistoryRepository _history;
    private readonly ILocalizationService _localization;
    private readonly IClock _clock;
    private readonly List<WorkTask> _allTasks = [];
    private readonly Dictionary<Guid, int> _breakCounts = [];

    /// <summary>Erzeugt das ViewModel.</summary>
    /// <param name="repository">Persistenz der Aufgaben.</param>
    /// <param name="history">Quelle für die Zahl der Pausen je Aufgabe.</param>
    /// <param name="localization">Liefert Beschriftungen und Textvorlagen.</param>
    /// <param name="clock">Zeitquelle für Anlage und Abschluss.</param>
    public WorkTaskListViewModel(
        IWorkTaskRepository repository,
        IBreakHistoryRepository history,
        ILocalizationService localization,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(clock);

        _repository = repository;
        _history = history;
        _localization = localization;
        _clock = clock;

        // Reihenfolge zählt: Das Setzen von SearchText löst den Filterlauf aus, und
        // der greift auf die Sammlung zu.
        VisibleTasks = [];
        SearchText = string.Empty;
        NewTaskText = string.Empty;
    }

    /// <summary>
    /// Wird gemeldet, sobald sich an den Aufgaben etwas geändert hat.
    /// </summary>
    /// <remarks>
    /// Ein Ereignis statt eines injizierten Dienstes: Wer die Meldung braucht — der
    /// Träger der laufenden Aufgabe und das Symbol im Infobereich — hängt sich in der
    /// Composition Root daran. Das ViewModel bleibt davon frei und ohne diese Teile
    /// testbar.
    /// </remarks>
    public event EventHandler? TasksChanged;

    /// <summary>Die nach Suche und Filter sichtbaren Aufgaben, neueste zuerst.</summary>
    public ObservableCollection<WorkTaskListItem> VisibleTasks { get; }

    /// <summary>Der eingegebene Suchtext; wirkt beim Tippen.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; }

    /// <summary>Text für eine neue Aufgabe.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewTaskText { get; set; }

    /// <summary>Der gewählte Filter.</summary>
    [ObservableProperty]
    public partial WorkTaskFilter Filter { get; set; }

    /// <summary>Wahr, wenn noch keine Aufgabe angelegt wurde.</summary>
    public bool ShowEmpty => _allTasks.Count == 0;

    /// <summary>Wahr, wenn Suche oder Filter alles ausgeblendet haben.</summary>
    public bool ShowNoResults => _allTasks.Count > 0 && VisibleTasks.Count == 0;

    /// <summary>Wahr, solange Aufgaben zu sehen sind.</summary>
    public bool HasResults => VisibleTasks.Count > 0;

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

    /// <summary>Bindung des Chips "Alle".</summary>
    public bool IsFilterAll
    {
        get => Filter == WorkTaskFilter.All;
        set => ToggleFilter(WorkTaskFilter.All, value, nameof(IsFilterAll));
    }

    /// <summary>Bindung des Chips "Offen".</summary>
    public bool IsFilterOpen
    {
        get => Filter == WorkTaskFilter.Open;
        set => ToggleFilter(WorkTaskFilter.Open, value, nameof(IsFilterOpen));
    }

    /// <summary>Bindung des Chips "Erledigt".</summary>
    public bool IsFilterCompleted
    {
        get => Filter == WorkTaskFilter.Completed;
        set => ToggleFilter(WorkTaskFilter.Completed, value, nameof(IsFilterCompleted));
    }

    /// <summary>
    /// Lädt die Aufgaben und zählt die Pausen, die an ihnen hingen.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkTask> tasks = await _repository.LoadAllAsync(cancellationToken).ConfigureAwait(true);
        await CountBreaksAsync(cancellationToken).ConfigureAwait(true);

        _allTasks.Clear();
        // Neueste zuerst: Woran gerade gearbeitet wird, steht oben.
        _allTasks.AddRange(tasks.OrderByDescending(task => task.CreatedAt));

        ApplyFilter();
    }

    /// <summary>Aktualisiert die Texte nach einem Sprachwechsel.</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public Task RefreshTextsAsync(CancellationToken cancellationToken = default)
    {
        OnPropertyChanged(string.Empty);
        return LoadAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        WorkTask task = WorkTask.Create(NewTaskText, _clock.UtcNow);
        await _repository.SaveAsync(task).ConfigureAwait(true);

        NewTaskText = string.Empty;
        await ReloadAndNotifyAsync().ConfigureAwait(true);
    }

    private bool CanAdd() => !string.IsNullOrWhiteSpace(NewTaskText);

    [RelayCommand]
    private async Task ToggleCompletedAsync(WorkTaskListItem? item)
    {
        if (item is null || Find(item.Id) is not { } task)
        {
            return;
        }

        WorkTask updated = task.IsCompleted ? task.Reopen() : task.Complete(_clock.UtcNow);
        await _repository.SaveAsync(updated).ConfigureAwait(true);
        await ReloadAndNotifyAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteAsync(WorkTaskListItem? item)
    {
        if (item is null)
        {
            return;
        }

        _ = await _repository.DeleteAsync(item.Id).ConfigureAwait(true);
        await ReloadAndNotifyAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private static void StartEdit(WorkTaskListItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.EditText = item.Text;
        item.IsEditing = true;
    }

    [RelayCommand]
    private async Task CommitEditAsync(WorkTaskListItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsEditing = false;

        // Ein leerer Text würde die Aufgabe namenlos machen; dann bleibt sie, wie sie war.
        if (string.IsNullOrWhiteSpace(item.EditText) || Find(item.Id) is not { } task)
        {
            item.EditText = item.Text;
            return;
        }

        await _repository.SaveAsync(task.WithText(item.EditText)).ConfigureAwait(true);
        await ReloadAndNotifyAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private static void CancelEdit(WorkTaskListItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.EditText = item.Text;
        item.IsEditing = false;
    }

    [RelayCommand]
    private void ResetSearch()
    {
        SearchText = string.Empty;
        Filter = WorkTaskFilter.All;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(WorkTaskFilter value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterOpen));
        OnPropertyChanged(nameof(IsFilterCompleted));
    }

    private async Task ReloadAndNotifyAsync()
    {
        await LoadAsync().ConfigureAwait(true);
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private WorkTask? Find(Guid id) => _allTasks.Find(task => task.Id == id);

    // Ein Filter ist immer gewählt. Klickt jemand den aktiven Chip erneut an, meldet
    // die Bindung "abgewählt" — das wird zurückgesetzt, sonst stünde die Leiste ohne
    // Auswahl da und der Zustand der Liste wäre nicht mehr abzulesen.
    private void ToggleFilter(WorkTaskFilter target, bool isChecked, string propertyName)
    {
        if (isChecked)
        {
            Filter = target;
            return;
        }

        OnPropertyChanged(propertyName);
    }

    private void ApplyFilter()
    {
        string needle = SearchText.Trim();

        VisibleTasks.Clear();
        foreach (WorkTask task in _allTasks.Where(task => Matches(task, needle)))
        {
            VisibleTasks.Add(ToListItem(task));
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private bool Matches(WorkTask task, string needle)
    {
        bool matchesFilter = Filter switch
        {
            WorkTaskFilter.Open => !task.IsCompleted,
            WorkTaskFilter.Completed => task.IsCompleted,
            _ => true,
        };

        return matchesFilter
            && (needle.Length == 0
                || task.Text.Contains(needle, StringComparison.CurrentCultureIgnoreCase));
    }

    private async Task CountBreaksAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<BreakSession> sessions = await _history.LoadAllAsync(cancellationToken).ConfigureAwait(true);

        _breakCounts.Clear();
        foreach (IGrouping<Guid, BreakSession> group in sessions
            .Where(session => session.TaskId.HasValue)
            .GroupBy(session => session.TaskId!.Value))
        {
            _breakCounts[group.Key] = group.Count();
        }
    }

    private WorkTaskListItem ToListItem(WorkTask task)
    {
        _ = _breakCounts.TryGetValue(task.Id, out int count);

        return new WorkTaskListItem(
            task.Id,
            task.Text,
            task.IsCompleted,
            task.CreatedAt.ToLocalTime().ToString("d", CultureInfo.CurrentCulture),
            count,
            string.Format(
                CultureInfo.CurrentCulture,
                _localization.GetText(SettingsTextKeys.TasksBreakCount),
                count));
    }
}
