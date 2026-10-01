using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>
/// Humo de las vistas de la Task 3 (biblioteca, colección y reordenar) en
/// tema claro y oscuro: cargan, resuelven estilos/bindings y hacen layout.
/// </summary>
public class Task3ViewTests
{
    private static readonly bool[] Themes = [false, true];

    [AvaloniaFact]
    public void LibraryView_shows_collections()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<LibraryViewModel>();
            var view = app.AssertShows<Hakufu.MVVM.View.LibraryView>();

            var names = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Ataque a los Titanes", names);
            Assert.Contains("1 colección · 2 tomos", names);
            // "Continuar leyendo" vive en Inicio, no aquí.
            Assert.DoesNotContain(names, n => n?.Contains("ontinuar", StringComparison.OrdinalIgnoreCase) == true);

            // Hay una colección: el estado vacío no se ve.
            var empty = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Tu estantería está vacía");
            Assert.False(empty.IsEffectivelyVisible);

            // Las portadas del montón de la tarjeta se cargan (Bitmap directo).
            Assert.Contains(view.GetVisualDescendants().OfType<Image>(), i => i.Source is not null);
        }
    }

    [AvaloniaFact]
    public void CollectionDetailView_shows_mangas()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<CollectionDetailViewModel>(app.SampleCollection.Id);
            var view = app.AssertShows<Hakufu.MVVM.View.CollectionDetailView>();

            var grid = view.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.UniformGrid>().Single();
            var vm   = (CollectionDetailViewModel)view.DataContext!;
            Assert.Equal(vm.ItemsPerRow, grid.Columns);
            Assert.Equal(2, grid.Children.Count);

            // "Tomo 1" va por la página 2 de 3: su barra de progreso se pinta.
            var bars = view.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()
                           .Where(r => r.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Left);
            Assert.Contains(bars, r => r.Bounds.Width > 0);

            vm.ItemsPerRow = 4;
            app.Pump();
            Assert.Equal(4, grid.Columns);
        }
    }

    [AvaloniaFact]
    public void LibraryView_asks_for_a_folder_when_there_is_none()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark, withLibrary: false);
            app.Root.Navigation.NavigateTo<LibraryViewModel>();
            var view = app.AssertShows<Hakufu.MVVM.View.LibraryView>();

            var texts = view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible)
                            .Select(t => t.Text).ToList();
            Assert.Contains("Elige la carpeta donde tienes tus mangas", texts);
            Assert.DoesNotContain("Tu estantería está vacía", texts);
            Assert.Contains(view.GetVisualDescendants().OfType<Button>(),
                            b => b.IsEffectivelyVisible && b.Content as string == "Elegir carpeta…");
        }
    }

    [AvaloniaFact]
    public void LibraryView_header_offers_folder_actions_instead_of_editing()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<LibraryViewModel>();
        var view = app.AssertShows<Hakufu.MVVM.View.LibraryView>();

        var buttons = view.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible)
                          .Select(b => b.Content as string).ToList();
        Assert.Contains("Abrir carpeta", buttons);
        Assert.Contains("Actualizar", buttons);
        Assert.DoesNotContain("Nueva colección", buttons);
        Assert.DoesNotContain("Seleccionar", buttons);
    }

    [AvaloniaFact]
    public void ReorderMangaView_opens_as_modal()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            var mangas = app.Root.Library.GetMangasInCollection(app.SampleCollection.Id).ToList();
            var vm = new ReorderMangaViewModel(mangas, app.Root.Dialog, () => Task.CompletedTask);
            var view = app.AssertModal<Hakufu.MVVM.View.ReorderMangaView>(vm);

            var titles = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Tomo 1", titles);
        }
    }
}
