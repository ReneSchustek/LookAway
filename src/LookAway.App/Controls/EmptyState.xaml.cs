using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LookAway.App.Controls;

/// <summary>
/// Erklärt eine leere Liste.
/// </summary>
/// <remarks>
/// Ein Leerzustand, der beide Lagen mit demselben Satz beantwortet, schickt den
/// Benutzer auf die Suche nach fehlenden Daten, obwohl nur ein Filter greift.
/// Deshalb trennt die Ansicht „nichts vorhanden" von „nichts gefunden" und
/// reicht hier den passenden Text herein.
/// </remarks>
internal sealed partial class EmptyState : UserControl
{
    /// <summary>Zeichen aus „Segoe MDL2 Assets" — dieselbe Quelle wie im System.</summary>
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(EmptyState),
        new PropertyMetadata(""));

    /// <summary>Überschrift des Leerzustands.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(EmptyState),
        new PropertyMetadata(string.Empty));

    /// <summary>Erklärender Satz darunter.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(EmptyState),
        new PropertyMetadata(string.Empty));

    /// <summary>Optionale Aktion, etwa eine Schaltfläche zum Zurücksetzen der Suche.</summary>
    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(
        nameof(ActionContent),
        typeof(object),
        typeof(EmptyState),
        new PropertyMetadata(null));

    /// <summary>Erzeugt den Leerzustand.</summary>
    public EmptyState() => InitializeComponent();

    /// <inheritdoc cref="GlyphProperty" />
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <inheritdoc cref="TitleProperty" />
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="TextProperty" />
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="ActionContentProperty" />
    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
}
