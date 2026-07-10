using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LookAway.App.Converters;

/// <summary>
/// Wandelt einen <see cref="bool"/> in <see cref="Visibility"/>:
/// <c>true</c> → <see cref="Visibility.Visible"/>, sonst <see cref="Visibility.Collapsed"/>.
/// </summary>
internal sealed partial class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}
