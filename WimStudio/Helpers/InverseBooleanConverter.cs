using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WimStudio.Helpers;

/// <summary>
/// Negiert einen Boolean. Wird der Zieltyp Visibility erkannt,
/// wird zusätzlich in Visibility konvertiert (true -> Collapsed, false -> Visible).
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var inverted = value is bool b && !b;

        if (targetType == typeof(Visibility))
            return inverted ? Visibility.Visible : Visibility.Collapsed;

        return inverted;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
