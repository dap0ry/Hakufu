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

    /// <summary>
    /// Si está puesto, un cambio de tema de verdad (claro ↔ oscuro) se hace a
    /// través de él: recibe el tema nuevo y el cambio, y decide cuándo aplicarlo.
    /// MainWindow lo usa para la transición de tinta (foto del tema viejo →
    /// cambio → animación). Sin él (arranque, tests) el tema se aplica al momento.
    /// </summary>
    public Action<AppTheme, Action>? Transition { get; set; }

    public void SetTheme(AppTheme theme)
    {
        var changed = theme != CurrentTheme;
        CurrentTheme = theme;
        if (changed && Transition is { } transition)
            transition(theme, () => Apply(theme));
        else
            Apply(theme);
    }

    private static void Apply(AppTheme theme)
    {
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
