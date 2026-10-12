using Avalonia.Headless.XUnit;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Páginas en orden natural y el progreso guardado con el orden ordinal de hasta la 0.12.</summary>
public class PageOrderTests
{
    // 12 páginas sin ceros delante. Orden ordinal (el viejo): 1, 10, 11, 12, 2, 3… 9.
    private static readonly string[] Pages = Enumerable.Range(1, 12).Select(n => $"{n}.png").ToArray();

    [Fact]
    public void Old_progress_points_to_the_same_page_in_the_new_order()
    {
        Assert.Equal(1, PageOrder.FromOrdinal(Pages, 4));  // la 5.ª del orden viejo era 2.png
        Assert.Equal(9, PageOrder.FromOrdinal(Pages, 1));  // la 2.ª era 10.png
        Assert.Equal(0, PageOrder.FromOrdinal(Pages, 0));
        // Terminado sigue terminado: en el orden viejo ya se habían pasado todas.
        Assert.Equal(11, PageOrder.FromOrdinal(Pages, 11));
    }

    [Fact]
    public async Task Opening_a_volume_moves_old_progress_once_and_saves_it()
    {
        using var tmp = new TempDataDir();
        var cbz = Fixtures.MakeCbz(Path.Combine(tmp.Root, "Mis mangas", "Berserk"), "Tomo 1.cbz", Pages);
        var repo = new JsonDataRepository();
        var manga = new Manga { Title = "Tomo 1", FilePath = cbz, TotalPages = 12 };
        repo.Current.Mangas.Add(manga);
        // Progreso de la 0.12: sin NaturalOrder (en data.json no existía).
        repo.Current.Progress.Add(new ReadingProgress { MangaId = manga.Id, CurrentPage = 4 });
        var library = new LibraryService(repo);

        var p = library.GetProgressForReading(manga)!;
        Assert.Equal(1, p.CurrentPage);
        Assert.True(p.NaturalOrder);
        Assert.Equal(1, library.GetProgressForReading(manga)!.CurrentPage); // solo una vez

        await repo.SaveAsync();
        var again = new JsonDataRepository();
        await again.LoadAsync();
        var saved = Assert.Single(again.Current.Progress);
        Assert.Equal(1, saved.CurrentPage);
        Assert.True(saved.NaturalOrder);
    }

    [Fact]
    public async Task New_progress_is_already_in_the_new_order()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        var manga = new Manga { Title = "Tomo 1", FilePath = Path.Combine(tmp.Root, "no está.cbz") };
        repo.Current.Mangas.Add(manga);
        var library = new LibraryService(repo);

        await library.SaveProgressAsync(manga.Id, 4);

        Assert.True(library.GetProgress(manga.Id)!.NaturalOrder);
        Assert.Equal(4, library.GetProgressForReading(manga)!.CurrentPage);
    }

    // «Seguir leyendo» en Inicio abre el lector donde iba, ya en el orden nuevo
    // (el lector empieza una página antes de la guardada).
    [AvaloniaFact]
    public void Continue_reading_opens_old_progress_on_the_same_page()
    {
        using var app = ViewSmoke.Start();
        var store = app.Root.Repo.Current;
        var cbz = Fixtures.MakeCbz(Path.Combine(store.LibraryRoot, app.SampleCollection.Name), "Doce.cbz", Pages);
        var manga = new Manga { Title = "Doce", FilePath = cbz, TotalPages = 12 };
        store.Mangas.Add(manga);
        // Iba por 10.png: la 2.ª del orden viejo, la 10.ª del nuevo.
        store.Progress.Add(new ReadingProgress { MangaId = manga.Id, CurrentPage = 1, LastRead = DateTime.Now.AddMinutes(1) });

        app.Root.Navigation.NavigateTo<HomeViewModel>();
        var home = Assert.IsType<HomeViewModel>(app.Root.Navigation.CurrentViewModel);
        home.ContinueReadingCommand.Execute(null);
        app.Pump();

        var reader = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        Assert.Equal(8, reader.CurrentPage); // 9.png, justo antes de 10.png
    }

    // Sin el archivo (disco desconectado) no se puede saber: se deja para cuando esté.
    [Fact]
    public void Old_progress_of_a_missing_file_waits()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        var manga = new Manga { Title = "Tomo 1", FilePath = Path.Combine(tmp.Root, "no está.cbz") };
        repo.Current.Mangas.Add(manga);
        repo.Current.Progress.Add(new ReadingProgress { MangaId = manga.Id, CurrentPage = 4 });

        var p = new LibraryService(repo).GetProgressForReading(manga)!;

        Assert.Equal(4, p.CurrentPage);
        Assert.False(p.NaturalOrder);
    }
}
