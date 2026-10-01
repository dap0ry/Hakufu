using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>Ajustes → Lectura: se guardan y el lector los respeta al abrir un manga.</summary>
public class ReaderSettingsTests
{
    private static ReaderViewModel OpenReader(ViewSmoke app)
    {
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.AssertShows<ReaderView>();
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        var sw = Stopwatch.StartNew();
        while (vm.PageLeft is null && sw.ElapsedMilliseconds < 5000) app.Pump();
        return vm;
    }

    [AvaloniaFact]
    public void Settings_screen_shows_and_saves_reader_preferences()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        app.AssertShows<SettingsView>();
        var settings = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);

        settings.TwoPageByDefault  = true;
        settings.OpenInZenMode     = true;
        settings.PageTurnAnimation = false;
        settings.SpeedSlowCommand.Execute(null);

        var saved = app.Root.Repo.Current.Reader;
        Assert.True(saved.TwoPageByDefault);
        Assert.True(saved.OpenInZenMode);
        Assert.False(saved.PageTurnAnimation);
        Assert.Equal("slow", saved.PageTurnSpeed);
        Assert.True(settings.IsSpeedSlow);
        Assert.False(settings.IsSpeedNormal);
    }

    [AvaloniaFact]
    public void Reader_opens_with_the_saved_preferences()
    {
        using var app = ViewSmoke.Start();
        var prefs = app.Root.Repo.Current.Reader;
        prefs.TwoPageByDefault  = true;
        prefs.OpenInZenMode     = true;
        prefs.PageTurnAnimation = false;

        var vm = OpenReader(app);
        Assert.True(vm.IsTwoPageMode);
        Assert.True(vm.IsZenMode);
        Assert.Equal(WindowState.FullScreen, app.Window.WindowState);

        // Sin animación la página cambia sin pintar nada en la capa de la hoja.
        var flipLayer = app.Window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "FlipLayer");
        vm.NextPageCommand.Execute(null);
        Assert.Empty(flipLayer.Children);
        Assert.Equal(2, vm.CurrentPage);
    }
}
