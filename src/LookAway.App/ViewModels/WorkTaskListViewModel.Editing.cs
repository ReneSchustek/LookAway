using CommunityToolkit.Mvvm.Input;

namespace LookAway.App.ViewModels;

/// <summary>
/// Bearbeiten direkt in der Zeile: Umbenennen beginnen, übernehmen oder verwerfen.
/// </summary>
internal sealed partial class WorkTaskListViewModel
{
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
}
