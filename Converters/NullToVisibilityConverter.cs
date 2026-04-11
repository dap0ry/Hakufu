using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Hakufu.Converters;

[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToVisibilityConverter : IValueConverter
{
    /// <summary>When true, Visible if null (inverse: placeholder visible when no value).</summary>
    public bool VisibleWhenNull { get; set; }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        return (VisibleWhenNull ? isNull : !isNull) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
