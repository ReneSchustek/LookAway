using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LookAway.App.Controls;

/// <summary>
/// Suchfeld der Gestaltungslinie: Lupe, Platzhalter, Löschen-Zeichen.
/// </summary>
/// <remarks>
/// Die Suche filtert beim Tippen, ohne Schaltfläche daneben — eine Suche, die erst
/// auf einen Klick reagiert, fühlt sich langsam an, auch wenn sie schnell ist.
/// Deshalb meldet <see cref="Text"/> jede Änderung sofort weiter.
/// </remarks>
internal sealed partial class SearchBox : UserControl
{
    /// <summary>Der eingegebene Suchtext.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(SearchBox),
        new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>Der Platzhalter; er benennt, was durchsucht wird.</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(SearchBox),
        new PropertyMetadata(string.Empty));

    /// <summary>Beschriftung des Löschen-Zeichens (Sprachausgabe und Kurzhinweis).</summary>
    public static readonly DependencyProperty ClearLabelProperty = DependencyProperty.Register(
        nameof(ClearLabel),
        typeof(string),
        typeof(SearchBox),
        new PropertyMetadata(string.Empty));

    /// <summary>Wahr, solange etwas im Feld steht.</summary>
    public static readonly DependencyProperty HasTextProperty = DependencyProperty.Register(
        nameof(HasText),
        typeof(bool),
        typeof(SearchBox),
        new PropertyMetadata(false));

    /// <summary>Erzeugt das Suchfeld.</summary>
    public SearchBox() => InitializeComponent();

    /// <inheritdoc cref="TextProperty" />
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="PlaceholderProperty" />
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <inheritdoc cref="ClearLabelProperty" />
    public string ClearLabel
    {
        get => (string)GetValue(ClearLabelProperty);
        set => SetValue(ClearLabelProperty, value);
    }

    /// <inheritdoc cref="HasTextProperty" />
    public bool HasText
    {
        get => (bool)GetValue(HasTextProperty);
        private set => SetValue(HasTextProperty, value);
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SearchBox box)
        {
            box.HasText = !string.IsNullOrEmpty(box.Text);
        }
    }

    // Escape leert das Feld — der kürzeste Weg zurück zur vollständigen Liste.
    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;
        Text = string.Empty;
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Text = string.Empty;

        // Nach dem Löschen bleibt der Fokus im Feld: Wer die Suche zurücksetzt,
        // will meist gleich neu tippen.
        _ = Input.Focus(FocusState.Programmatic);
    }
}
