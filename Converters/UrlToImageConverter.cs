using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Hakufu.Converters;

public class UrlToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource        = new Uri(url, UriKind.Absolute);
            img.CacheOption      = BitmapCacheOption.OnLoad;
            img.CreateOptions    = BitmapCreateOptions.IgnoreImageCache;
            img.EndInit();
            return img;
        }
        catch { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
