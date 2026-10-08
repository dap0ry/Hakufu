using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>MainView como vista única (iPhone/iPad).</summary>
public class MobileShellTests
{
    [AvaloniaFact]
    public void Main_view_is_compact_on_a_phone_but_not_on_a_tablet()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        Assert.True(app.View.IsCompact);
        var changes = 0;
        app.View.PropertyChanged += (_, e) => { if (e.Property == MainView.IsCompactProperty) changes++; };

        app.Host.Width = 1180;
        app.Host.Height = 820;
        app.Pump();
        Assert.False(app.View.IsCompact);
        Assert.Equal(1, changes); // se puede enlazar ({Binding $parent[local:MainView].IsCompact})
    }

    // iPad en vertical: no es compacto (eso es el iPhone) pero sí estrecho (< 900, el
    // mínimo de la ventana de escritorio): el escritorio nunca ve "narrow".
    [AvaloniaFact]
    public void Ipad_portrait_is_narrow_but_not_compact()
    {
        using var app = ViewSmoke.StartMobile(744, 1133);
        Assert.Contains("narrow", app.View.Classes);
        Assert.DoesNotContain("compact", app.View.Classes);

        app.Host.Width = 390;
        app.Pump();
        Assert.Contains("narrow", app.View.Classes);
        Assert.Contains("compact", app.View.Classes);
    }

    // Sin nada empezado, en una columna no queda el hueco de «Continuar leyendo».
    [AvaloniaFact]
    public void Home_on_a_phone_has_no_empty_continue_bar()
    {
        using var app = ViewSmoke.Start(withLibrary: false, mobile: new Avalonia.Size(390, 844));
        var home = app.AssertShows<Hakufu.MVVM.View.HomeView>();

        var panel = home.FindControl<Avalonia.Controls.Grid>("ContinuePanel")!;
        Assert.False(panel.IsEffectivelyVisible);
    }

    // En el lector, lo que queda detrás de la barra de estado (zona segura) va oscuro como el lector.
    [AvaloniaFact]
    public void Reader_paints_the_safe_area_dark()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        var appBackground = app.View.Background;

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.Pump();
        Assert.True(app.View.TryFindResource("ReaderBackground", out var reader));
        Assert.Same(reader, app.View.Background);

        app.Root.Navigation.NavigateTo<HomeViewModel>();
        app.Pump();
        Assert.Same(appBackground, app.View.Background);
    }

    [AvaloniaFact]
    public void Shows_home_on_its_own()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        app.AssertShows<Hakufu.MVVM.View.HomeView>();
    }

    [AvaloniaFact]
    public void Zen_mode_and_leaving_the_reader_are_reported()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        var events = new List<string>();
        app.View.ZenModeChanged += (_, zen) => events.Add(zen ? "zen" : "no-zen");
        app.View.ReaderClosed   += (_, wasZen) => events.Add(wasZen ? "closed-in-zen" : "closed");

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.Pump();
        var reader = Assert.IsType<ReaderViewModel>(app.View.Reader);
        reader.IsZenMode = true;
        app.Root.Navigation.NavigateTo<HomeViewModel>();
        app.Pump();

        Assert.Null(app.View.Reader);
        Assert.Equal(["zen", "closed-in-zen"], events);
    }
}

public class CompactColumnsTests
{
    [Fact]
    public void Phone_uses_three_columns_and_desktop_keeps_the_chosen_size()
    {
        var conv = new Hakufu.Converters.CompactColumnsConverter();
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        Assert.Equal(3, conv.Convert([7, true], typeof(int), null, culture));
        Assert.Equal(7, conv.Convert([7, false], typeof(int), null, culture));
        Assert.Equal(1, conv.Convert([null, false], typeof(int), null, culture));
    }
}
