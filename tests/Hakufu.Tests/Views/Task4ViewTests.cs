using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

public class Task4ViewTests
{
    /// <summary>Las páginas se decodifican en segundo plano: bombea la cola de UI hasta que se cumpla.</summary>
    private static void PumpUntil(ViewSmoke app, Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            app.Pump();
        Assert.True(condition(), "La condición no se cumplió a tiempo.");
    }

    private static void Press(ViewSmoke app, PhysicalKey key)
    {
        app.Window.KeyPressQwerty(key, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(key, RawInputModifiers.None);
        app.Pump();
    }

    private static ReaderViewModel OpenReader(ViewSmoke app)
    {
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.AssertShows<ReaderView>();
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        PumpUntil(app, () => vm.PageLeft is not null || vm.PageRight is not null);
        return vm;
    }

    [AvaloniaFact]
    public void Reader_view_shows_and_loads_the_first_page()
    {
        using var app = ViewSmoke.Start();
        var vm = OpenReader(app);

        Assert.Equal(0, vm.CurrentPage);
        Assert.NotNull(vm.PageLeft);
        Assert.Equal("1 / 3", vm.PageDisplay);
    }

    [AvaloniaFact]
    public void Reader_arrow_keys_turn_pages()
    {
        using var app = ViewSmoke.Start();
        var vm = OpenReader(app);

        Press(app, PhysicalKey.ArrowRight);
        Assert.Equal(1, vm.CurrentPage);

        Press(app, PhysicalKey.Space);
        Assert.Equal(2, vm.CurrentPage);

        Press(app, PhysicalKey.ArrowRight);          // ya en la última: no se mueve
        Assert.Equal(2, vm.CurrentPage);

        Press(app, PhysicalKey.ArrowLeft);
        Assert.Equal(1, vm.CurrentPage);
        PumpUntil(app, () => vm.PageLeft is not null);
    }

    [AvaloniaFact]
    public void Reader_keys_toggle_two_page_and_zen_mode()
    {
        using var app = ViewSmoke.Start();
        var vm = OpenReader(app);

        Press(app, PhysicalKey.Digit2);
        Assert.True(vm.IsTwoPageMode);
        PumpUntil(app, () => vm.PageRight is not null);

        Press(app, PhysicalKey.Digit1);
        Assert.False(vm.IsTwoPageMode);

        Press(app, PhysicalKey.F);
        Assert.True(vm.IsZenMode);

        Press(app, PhysicalKey.Escape);
        Assert.False(vm.IsZenMode);
    }

    [AvaloniaFact]
    public void Profile_view_shows_in_both_themes()
    {
        foreach (var dark in new[] { false, true })
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.SampleCollection.IsFavorite = true;   // para pintar también "Colecciones favoritas"
            app.Root.Navigation.NavigateTo<ProfileViewModel>();
            var view = app.AssertShows<ProfileView>();

            var vm = Assert.IsType<ProfileViewModel>(app.Root.Navigation.CurrentViewModel);
            Assert.True(vm.HasTopMangas);            // "Tomo 2" está marcado como favorito
            Assert.True(vm.HasFavoriteCollections);
            Assert.True(vm.HasRecentActivity);       // "Tomo 1" está en el historial
            Assert.True(vm.HasCollectionStats);      // "Tomo 1" va por la página 2

            // La barra de "Páginas por colección" tiene ancho (MultiBinding PercentageWidth)
            var bar = view.GetVisualDescendants().OfType<Border>()
                          .FirstOrDefault(b => b.Height == 10 && b.HorizontalAlignment == HorizontalAlignment.Left);
            Assert.NotNull(bar);
            Assert.True(bar!.Bounds.Width > 0, "La barra de estadísticas no tiene ancho.");

            // Pantalla de historial completo
            vm.ViewAllHistoryCommand.Execute(null);
            app.Pump();
            Assert.True(vm.ShowingAllHistory);
            Assert.NotEmpty(vm.FullHistory);
            app.AssertShows<ProfileView>();

            vm.BackToProfileCommand.Execute(null);
            app.Pump();
            Assert.False(vm.ShowingAllHistory);
        }
    }
}
