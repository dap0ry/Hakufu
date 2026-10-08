using Avalonia.Headless.XUnit;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

public class ShellTests
{
    [AvaloniaFact]
    public void Main_window_starts_on_home_in_both_themes()
    {
        foreach (var dark in new[] { false, true })
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            Assert.IsType<HomeViewModel>(app.Root.Navigation.CurrentViewModel);
            Assert.True(app.Window.IsVisible);
        }
    }

    [AvaloniaFact]
    public void Every_navigable_view_model_can_be_created()
    {
        using var app = ViewSmoke.Start();
        var nav = app.Root.Navigation;

        nav.NavigateTo<LibraryViewModel>();
        nav.NavigateTo<CollectionDetailViewModel>(app.SampleCollection.Id);
        nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        nav.NavigateTo<ProfileViewModel>();
        nav.NavigateTo<SettingsViewModel>();
        nav.NavigateTo<HelpViewModel>();
        nav.NavigateTo<BackupViewModel>();
        app.Pump();

        Assert.IsType<BackupViewModel>(nav.CurrentViewModel);
    }
}

public class AppNameTests
{
    // macOS enseña Application.Name en la barra de menús ("Avalonia Application" si no se pone).
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void The_app_is_called_Hakufu_in_the_menu_bar()
        => Assert.Equal("Hakufu", Avalonia.Application.Current!.Name);
}
