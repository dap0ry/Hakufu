using Avalonia.Headless.XUnit;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>Humo de las vistas de la Task 2 (Inicio, Ayuda, Ajustes, Personalizar) en tema claro y oscuro.</summary>
public class Task2ViewTests
{
    private static readonly bool[] Themes = [false, true];

    [AvaloniaFact]
    public void HomeView_loads_and_backup_tile_navigates_to_backup()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<HomeViewModel>();
            app.AssertShows<Hakufu.MVVM.View.HomeView>();

            var home = Assert.IsType<HomeViewModel>(app.Root.Navigation.CurrentViewModel);
            Assert.True(home.HasLastManga);

            home.NavBackupCommand.Execute(null);
            app.Pump();
            Assert.IsType<BackupViewModel>(app.Root.Navigation.CurrentViewModel);
        }
    }

    [AvaloniaFact]
    public void HelpView_loads()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<HelpViewModel>();
            app.AssertShows<Hakufu.MVVM.View.HelpView>();
        }
    }

    [AvaloniaFact]
    public void SettingsView_loads()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<SettingsViewModel>();
            app.AssertShows<Hakufu.MVVM.View.SettingsView>();
        }
    }

    [AvaloniaFact]
    public void CustomizationView_loads()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<CustomizationViewModel>();
            app.AssertShows<Hakufu.MVVM.View.CustomizationView>();
        }
    }
}
