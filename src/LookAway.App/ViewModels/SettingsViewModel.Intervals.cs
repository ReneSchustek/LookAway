using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;

namespace LookAway.App.ViewModels;

/// <summary>
/// Seite „Intervalle“: eigene Arbeits- und Pausendauern, ihre Prüfung gegen die Grenzen des
/// gewählten Modells und der Bereichshinweis.
/// </summary>
internal sealed partial class SettingsViewModel
{
    [ObservableProperty]
    public partial bool UseCustomDurations { get; set; }

    [ObservableProperty]
    public partial int WorkMinutes { get; set; }

    [ObservableProperty]
    public partial int BreakMinutes { get; set; }

    [ObservableProperty]
    public partial string? WorkError { get; set; }

    [ObservableProperty]
    public partial string? BreakError { get; set; }

    /// <summary>Untere Grenze der Arbeitsdauer (Minuten) für das aktive Modell.</summary>
    public int WorkMinMinutes { get; private set; } = (int)BreakInterval.MinWorkDuration.TotalMinutes;

    /// <summary>Obere Grenze der Arbeitsdauer (Minuten) für das aktive Modell.</summary>
    public int WorkMaxMinutes { get; private set; } = (int)BreakInterval.MaxWorkDuration.TotalMinutes;

    /// <summary>Untere Grenze der Pausendauer (Minuten).</summary>
    public int BreakMinMinutes => (int)BreakInterval.MinBreakDuration.TotalMinutes;

    /// <summary>Obere Grenze der Pausendauer (Minuten).</summary>
    public int BreakMaxMinutes => (int)BreakInterval.MaxBreakDuration.TotalMinutes;

    /// <summary>Wahr, wenn das aktive Modell die Arbeitsdauer nur in einem Bereich erlaubt.</summary>
    public bool HasWorkRange { get; private set; }

    /// <summary>Hinweis auf den erlaubten Arbeitsdauer-Bereich (leer, wenn unbeschränkt).</summary>
    public string WorkRangeText => HasWorkRange
        ? string.Format(CultureInfo.CurrentCulture, _localization.GetText(SettingsTextKeys.WorkRangeHint), WorkMinMinutes, WorkMaxMinutes)
        : string.Empty;

    private void LoadDurations(Settings settings)
    {
        if (settings.CustomDurations is { } custom)
        {
            UseCustomDurations = true;
            WorkMinutes = custom.WorkMinutes;
            BreakMinutes = custom.BreakMinutes;
            return;
        }

        UseCustomDurations = false;
        BreakInterval defaults = BreakModelRegistry.GetDefault(settings.BreakModel);
        WorkMinutes = (int)defaults.WorkDuration.TotalMinutes;
        BreakMinutes = (int)defaults.BreakDuration.TotalMinutes;
    }

    private void StoreDurations(Settings settings)
        => settings.CustomDurations = UseCustomDurations
            ? new CustomDurations { WorkMinutes = WorkMinutes, BreakMinutes = BreakMinutes }
            : null;

    private void Validate()
    {
        if (!UseCustomDurations)
        {
            WorkError = null;
            BreakError = null;
            return;
        }

        WorkError = WorkMinutes < WorkMinMinutes || WorkMinutes > WorkMaxMinutes
            ? string.Format(CultureInfo.CurrentCulture, _localization.GetText(SettingsTextKeys.ValidationWorkRange), WorkMinMinutes, WorkMaxMinutes)
            : null;

        BreakError = BreakMinutes < BreakMinMinutes || BreakMinutes > BreakMaxMinutes
            ? string.Format(CultureInfo.CurrentCulture, _localization.GetText(SettingsTextKeys.ValidationBreakRange), BreakMinMinutes, BreakMaxMinutes)
            : null;
    }

    private void UpdateModelRanges(BreakModel model)
    {
        BreakInterval defaults = BreakModelRegistry.GetDefault(model);
        WorkDurationRange? range = BreakModelRegistry.GetWorkDurationRange(model);

        HasWorkRange = range.HasValue;
        WorkMinMinutes = range.HasValue
            ? (int)range.Value.Min.TotalMinutes
            : (int)BreakInterval.MinWorkDuration.TotalMinutes;
        WorkMaxMinutes = range.HasValue
            ? (int)range.Value.Max.TotalMinutes
            : (int)(defaults.MaxLimit ?? BreakInterval.MaxWorkDuration).TotalMinutes;

        OnPropertyChanged(nameof(WorkMinMinutes));
        OnPropertyChanged(nameof(WorkMaxMinutes));
        OnPropertyChanged(nameof(HasWorkRange));
        OnPropertyChanged(nameof(WorkRangeText));
    }

    partial void OnSelectedModelOptionChanged(SettingsOption<BreakModel>? value)
    {
        if (_isInitializing || value is null)
        {
            return;
        }

        BreakModels.SetActiveModel(value.Value);
        UpdateModelRanges(value.Value);

        // Bei Modellwechsel die Dauern auf die Modell-Vorgaben zurücksetzen,
        // damit zuvor eingegebene Werte nicht außerhalb des neuen Bereichs liegen.
        BreakInterval defaults = BreakModelRegistry.GetDefault(value.Value);
        WorkMinutes = (int)defaults.WorkDuration.TotalMinutes;
        BreakMinutes = (int)defaults.BreakDuration.TotalMinutes;

        Validate();
    }

    partial void OnUseCustomDurationsChanged(bool value) => Validate();

    partial void OnWorkMinutesChanged(int value) => Validate();

    partial void OnBreakMinutesChanged(int value) => Validate();

    partial void OnWorkErrorChanged(string? value) => NotifyPersistCommands();

    partial void OnBreakErrorChanged(string? value) => NotifyPersistCommands();
}
