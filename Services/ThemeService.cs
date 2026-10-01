using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace Hakufu.Services;

public class ThemeService : IThemeService
{
    private static readonly Uri LightThemeSource = new("avares://Hakufu/Assets/Themes/LightTheme.axaml");
    private static readonly Uri DarkThemeSource  = new("avares://Hakufu/Assets/Themes/DarkTheme.axaml");

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        var app = Application.Current!;

        // Los controles de Fluent (TextBox, ScrollBar, CheckBox…) siguen al
        // tema claro/oscuro de Avalonia; nuestros brushes, al diccionario [0].
        app.RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

        var newDict = new ResourceInclude(new Uri("avares://Hakufu/"))
        {
            Source = theme == AppTheme.Dark ? DarkThemeSource : LightThemeSource
        };

        var merged = app.Resources.MergedDictionaries;
        if (merged.Count > 0)
            merged[0] = newDict;
        else
            merged.Insert(0, newDict);
    }
}
