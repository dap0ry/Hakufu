using System.Globalization;
using System.Windows.Data;

namespace Hakufu.Converters;

/// <summary>Multi-binding converter: returns true if all values are equal.</summary>
public class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length >= 2 && Equals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
