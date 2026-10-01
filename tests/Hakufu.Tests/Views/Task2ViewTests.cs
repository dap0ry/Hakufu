using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
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
            var view = app.AssertShows<Hakufu.MVVM.View.SettingsView>();

            // Tarjeta "Carpeta de la biblioteca" con la ruta elegida y sus botones.
            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Carpeta de la biblioteca", texts);
            Assert.Contains(app.Root.Repo.Current.LibraryRoot, texts);
            var buttons = view.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string).ToList();
            Assert.Contains("Elegir carpeta…", buttons);
            Assert.Contains("Volver a leer", buttons);
            Assert.DoesNotContain("Gestionar espacio", buttons);
        }
    }

}
