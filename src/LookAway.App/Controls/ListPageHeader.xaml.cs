using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LookAway.App.Controls;

/// <summary>
/// Kopf einer Listen-Ansicht: Titel, erklärende Zeile, Primäraktion.
/// </summary>
internal sealed partial class ListPageHeader : UserControl
{
    /// <summary>Titel der Ansicht.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(ListPageHeader),
        new PropertyMetadata(string.Empty));

    /// <summary>Eine Zeile, die sagt, was man hier tut.</summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(string),
        typeof(ListPageHeader),
        new PropertyMetadata(string.Empty));

    /// <summary>Optionale Primäraktion rechts oben.</summary>
    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(
        nameof(ActionContent),
        typeof(object),
        typeof(ListPageHeader),
        new PropertyMetadata(null));

    /// <summary>Erzeugt den Seitenkopf.</summary>
    public ListPageHeader() => InitializeComponent();

    /// <inheritdoc cref="TitleProperty" />
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="SubtitleProperty" />
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <inheritdoc cref="ActionContentProperty" />
    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
}
