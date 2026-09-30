using System.Globalization;
using Avalonia.Data.Converters;

namespace Hakufu.Converters;

/// <summary>Multi-binding converter: returns true if all values are equal.</summary>
public class EqualityConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count >= 2 && Equals(values[0], values[1]);
}
