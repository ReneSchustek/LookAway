using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LookAway.App.Converters;

/// <summary>
/// Wandelt einen <see cref="bool"/> umgekehrt in <see cref="Visibility"/>:
/// <c>true</c> → <see cref="Visibility.Collapsed"/>, sonst <see cref="Visibility.Visible"/>.
/// </summary>
/// <remarks>
/// Für Stellen, an denen zwei Darstellungen einander ablösen — etwa Anzeige und
/// Bearbeitung derselben Aufgabe.
/// </remarks>
internal sealed partial class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is not Visibility.Visible;
}
