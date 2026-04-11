using System.Windows;

namespace Hakufu.Services;

public class ThemeService : IThemeService
{
    private const string LightThemeSource = "Assets/Themes/LightTheme.xaml";
    private const string DarkThemeSource  = "Assets/Themes/DarkTheme.xaml";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        var source = theme == AppTheme.Dark ? DarkThemeSource : LightThemeSource;
        var newDict = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
            merged[0] = newDict;
        else
            merged.Insert(0, newDict);
    }
}
