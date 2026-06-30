using CommunityToolkit.Mvvm.ComponentModel;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Ein auswählbarer Eintrag (Sprache, Pausenmodell) mit sprachabhängiger
/// Beschriftung. Die Beschriftung wird bei einem Sprachwechsel über
/// <see cref="RefreshLabel"/> neu berechnet, ohne den Eintrag zu ersetzen — so
/// bleibt die Auswahl in der gebundenen ComboBox erhalten.
/// </summary>
/// <typeparam name="T">Typ des zugrunde liegenden Werts.</typeparam>
public sealed class SettingsOption<T> : ObservableObject
{
    private readonly Func<string> _labelFactory;
    private string _label;

    /// <summary>
    /// Erzeugt einen Eintrag aus Wert und einer Funktion, die die aktuelle
    /// Beschriftung liefert.
    /// </summary>
    /// <param name="value">Der zugrunde liegende Wert.</param>
    /// <param name="labelFactory">Liefert die Beschriftung in der aktuellen Sprache.</param>
    public SettingsOption(T value, Func<string> labelFactory)
    {
        ArgumentNullException.ThrowIfNull(labelFactory);
        Value = value;
        _labelFactory = labelFactory;
        _label = labelFactory();
    }

    /// <summary>Der zugrunde liegende Wert des Eintrags.</summary>
    public T Value { get; }

    /// <summary>Die in der UI angezeigte, sprachabhängige Beschriftung.</summary>
    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    /// <summary>Berechnet die Beschriftung in der aktuellen Sprache neu.</summary>
    public void RefreshLabel() => Label = _labelFactory();
}
