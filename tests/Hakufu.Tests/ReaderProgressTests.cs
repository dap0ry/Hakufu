using Avalonia.Headless.XUnit;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

public class ReaderProgressTests
{
    // Doble página con un número par de páginas: el último pliego empieza en la
    // penúltima y ya enseña la última. CurrentPage se quedaba en la penúltima: el
    // tomo no salía como «Terminado», no contaba en Perfil y «siguiente» enseñaba
    // otra vez la última página sola.
    [AvaloniaFact]
    public void Two_page_mode_finishes_a_volume_with_an_even_page_count()
    {
        using var app = ViewSmoke.Start();
        var store = app.Root.Repo.Current;
        store.Reader.TwoPageByDefault = true;
        var dir = Path.Combine(store.LibraryRoot, app.SampleCollection.Name);
        var manga = new Manga { Title = "Cuatro", TotalPages = 4,
                                FilePath = Fixtures.MakeCbz(dir, "Cuatro.cbz", "1.png", "2.png", "3.png", "4.png") };
        store.Mangas.Add(manga);

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(manga, 0));
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        app.Pump();

        vm.NextPageCommand.Execute(null); // páginas 3 y 4: la última ya se ve
        app.Pump();

        Assert.False(vm.NextPageCommand.CanExecute(null));
        Assert.Contains(store.History, h => h.MangaId == manga.Id);
        vm.CloseReaderCommand.Execute(null);
        app.Pump();
        var progress = Assert.Single(store.Progress, p => p.MangaId == manga.Id);
        Assert.True(new MangaCardViewModel(manga, progress).IsFinished);
    }

    // En una sola página no cambia nada: se termina al llegar a la última.
    [AvaloniaFact]
    public void Single_page_mode_finishes_on_the_last_page()
    {
        using var app = ViewSmoke.Start();
        var store = app.Root.Repo.Current;
        var dir = Path.Combine(store.LibraryRoot, app.SampleCollection.Name);
        var manga = new Manga { Title = "Cuatro", TotalPages = 4,
                                FilePath = Fixtures.MakeCbz(dir, "Cuatro.cbz", "1.png", "2.png", "3.png", "4.png") };
        store.Mangas.Add(manga);

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(manga, 0));
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        app.Pump();

        vm.NextPageCommand.Execute(null);
        vm.NextPageCommand.Execute(null);
        Assert.DoesNotContain(store.History, h => h.MangaId == manga.Id);
        vm.NextPageCommand.Execute(null);
        app.Pump();

        Assert.False(vm.NextPageCommand.CanExecute(null));
        Assert.Contains(store.History, h => h.MangaId == manga.Id);
        Assert.Equal(3, Assert.Single(store.Progress, p => p.MangaId == manga.Id).CurrentPage);
    }
}
