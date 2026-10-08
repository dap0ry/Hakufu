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

        app.Host.Width = 1180;
        app.Host.Height = 820;
        app.Pump();
        Assert.False(app.View.IsCompact);
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
