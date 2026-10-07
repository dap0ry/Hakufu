using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>La biblioteca es una carpeta del usuario: subcarpetas = colecciones, archivos = tomos.</summary>
public class LibraryScannerTests
{
    private static async Task<JsonDataRepository> NewRepoAsync(string? root = null)
    {
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        if (root is not null) repo.Current.LibraryRoot = root;
        return repo;
    }

    private static string[] Snapshot(string dir) =>
        Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories).Order().ToArray();

    [Fact]
    public async Task Subfolders_are_collections_and_manga_files_are_volumes_in_natural_order()
    {
        using var tmp = new TempDataDir();
        var root = Path.Combine(tmp.Root, "Mis mangas");
        var berserk = Path.Combine(root, "Berserk");
        Fixtures.MakeCbz(berserk, "Tomo 10.cbz", "1.png");
        Fixtures.MakeCbz(berserk, "Tomo 2.cbz", "1.png");
        Fixtures.MakePdf(berserk, "Tomo 1.pdf");
        Fixtures.MakeCbz(berserk, "Tomo 3.CBR", "1.png");                // la extensión no distingue mayúsculas
        await File.WriteAllTextAsync(Path.Combine(berserk, "notas.txt"), "x");
        Fixtures.MakeCbz(berserk, ".oculto.cbz", "1.png");
        Fixtures.MakeCbz(Path.Combine(root, ".Trash"), "x.cbz", "1.png");
        // Carpetas con "_" delante = datos/copias (p. ej. _Hakufu en un USB), no colecciones.
        Fixtures.MakeCbz(Path.Combine(root, "_Hakufu"), "x.cbz", "1.png");
        Directory.CreateDirectory(Path.Combine(root, "Vacía"));
        Fixtures.MakeCbz(root, "Suelto.cbz", "1.png");
        var before = Snapshot(root);

        var repo = await NewRepoAsync(root);
        var result = await new LibraryScanner(repo).ScanAsync();

        Assert.True(result.Ok);
        var cols = repo.Current.Collections;
        Assert.Equal(["Berserk", "Vacía", LibraryScanner.LooseCollectionName], cols.Select(c => c.Name));
        var library = new LibraryService(repo);
        var volumes = LibraryService.SortMangas(library.GetMangasInCollection(cols[0].Id), "name").ToList();
        Assert.Equal(["Tomo 1", "Tomo 2", "Tomo 3", "Tomo 10"], volumes.Select(m => m.Title));
        Assert.Equal("Berserk/Tomo 10.cbz", volumes[3].RelativePath);
        Assert.Equal(Path.Combine(berserk, "Tomo 10.cbz"), volumes[3].FilePath);
        Assert.Empty(cols[1].MangaIds);
        Assert.Equal("Suelto", Assert.Single(library.GetMangasInCollection(cols[2].Id)).Title);
        Assert.Equal(root, library.GetCollectionFolder(cols[2].Id));
        Assert.Equal(berserk, library.GetCollectionFolder(cols[0].Id));
        // Páginas contadas en segundo plano para que el progreso funcione.
        Assert.All(volumes, m => Assert.Equal(1, m.TotalPages));
        // Solo lectura: la carpeta del usuario queda exactamente igual.
        Assert.Equal(before, Snapshot(root));
    }

    [Fact]
    public async Task No_loose_collection_without_loose_files()
    {
        using var tmp = new TempDataDir();
        var root = Path.Combine(tmp.Root, "Mis mangas");
        Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 1.cbz", "1.png");
        await File.WriteAllTextAsync(Path.Combine(root, "léeme.txt"), "x");

        var repo = await NewRepoAsync(root);
        await new LibraryScanner(repo).ScanAsync();

        Assert.Equal("Berserk", Assert.Single(repo.Current.Collections).Name);
    }

    [Fact]
    public async Task Rescan_keeps_ids_progress_favorites_and_order()
    {
        using var tmp = new TempDataDir();
        var root = Path.Combine(tmp.Root, "Mis mangas");
        Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 1.cbz", "1.png", "2.png");
        Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 2.cbz", "1.png");
        var repo = await NewRepoAsync(root);
        var scanner = new LibraryScanner(repo);
        await scanner.ScanAsync();

        var col   = Assert.Single(repo.Current.Collections);
        var tomo1 = repo.Current.Mangas.Single(m => m.Title == "Tomo 1");
        col.IsFavorite  = true;
        col.Description = "Guts";
        tomo1.IsFavorite  = true;
        tomo1.CustomOrder = 5;
        repo.Current.Progress.Add(new ReadingProgress { MangaId = tomo1.Id, CurrentPage = 1 });
        var ids = repo.Current.Mangas.Select(m => m.Id).Order().ToList();

        // Otra sesión: se recarga data.json y se vuelve a leer la carpeta.
        await repo.SaveAsync();
        var again = await NewRepoAsync();
        await new LibraryScanner(again).ScanAsync();

        Assert.Equal(ids, again.Current.Mangas.Select(m => m.Id).Order());
        var col2 = Assert.Single(again.Current.Collections);
        Assert.Equal(col.Id, col2.Id);
        Assert.True(col2.IsFavorite);
        Assert.Equal("Guts", col2.Description);
        var t1 = again.Current.Mangas.Single(m => m.Id == tomo1.Id);
        Assert.True(t1.IsFavorite);
        Assert.Equal(5, t1.CustomOrder);
        Assert.Equal(2, t1.TotalPages);
        Assert.Equal(1, Assert.Single(again.Current.Progress).CurrentPage);
    }

    [Fact]
    public async Task Added_and_removed_files_and_folders_are_picked_up()
    {
        using var tmp = new TempDataDir();
        var root = Path.Combine(tmp.Root, "Mis mangas");
        var gone = Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 1.cbz", "1.png");
        Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 2.cbz", "1.png");
        Fixtures.MakeCbz(Path.Combine(root, "Monster"), "Tomo 1.cbz", "1.png");
        var repo = await NewRepoAsync(root);
        var scanner = new LibraryScanner(repo);
        await scanner.ScanAsync();
        var goneId = repo.Current.Mangas.Single(m => m.FilePath == gone).Id;
        var keptId = repo.Current.Mangas.Single(m => m.RelativePath == "Berserk/Tomo 2.cbz").Id;
        repo.Current.Progress.Add(new ReadingProgress { MangaId = goneId, CurrentPage = 0 });

        // El usuario cambia cosas en su carpeta (esto lo hace el test, no Hakufu).
        File.Delete(gone);
        Directory.Delete(Path.Combine(root, "Monster"), recursive: true);
        Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 3.cbz", "1.png");
        Fixtures.MakeCbz(Path.Combine(root, "Vagabond"), "Tomo 1.cbz", "1.png");
        await scanner.ScanAsync();

        Assert.Equal(["Berserk", "Vagabond"], repo.Current.Collections.Select(c => c.Name));
        Assert.Equal(["Berserk/Tomo 2.cbz", "Berserk/Tomo 3.cbz", "Vagabond/Tomo 1.cbz"],
                     repo.Current.Mangas.Select(m => m.RelativePath).Order());
        Assert.Contains(repo.Current.Mangas, m => m.Id == keptId);
        Assert.DoesNotContain(repo.Current.Mangas, m => m.Id == goneId);
        Assert.Empty(repo.Current.Progress);
        Assert.Equal(2, repo.Current.Collections[0].MangaIds.Count);
    }

    // Lo más importante: quien actualiza desde la versión que copiaba los mangas
    // a <datos>/biblioteca conserva Ids, progreso y colecciones sin hacer nada.
    [Fact]
    public async Task Old_copy_folder_becomes_the_library_and_keeps_progress()
    {
        using var tmp = new TempDataDir();
        var repo = await NewRepoAsync();
        var store = repo.Current;
        var t1 = new Manga { Title = "Tomo 1", TotalPages = 2, IsFavorite = true,
                             FilePath = Fixtures.MakeCbz(Path.Combine(AppPaths.LibraryDir, "Berserk"), "Tomo 1.cbz", "1.png", "2.png") };
        // "Re:Zero" se copiaba a la carpeta "Re_Zero" (caracteres no válidos en Windows).
        var t2 = new Manga { Title = "Tomo 1", TotalPages = 1,
                             FilePath = Fixtures.MakeCbz(Path.Combine(AppPaths.LibraryDir, "Re_Zero"), "Tomo 1.cbz", "1.png") };
        var outside = new Manga { Title = "Fuera", FilePath = Fixtures.MakeCbz(Path.Combine(tmp.Root, "otra"), "x.cbz", "1.png") };
        var berserk = new Collection { Name = "Berserk", IsFavorite = true, Description = "Guts", MangaIds = [t1.Id] };
        var rezero  = new Collection { Name = "Re:Zero", MangaIds = [t2.Id, outside.Id] };
        store.Mangas.AddRange([t1, t2, outside]);
        store.Collections.AddRange([berserk, rezero]);
        store.Progress.Add(new ReadingProgress { MangaId = t1.Id, CurrentPage = 1 });
        store.Progress.Add(new ReadingProgress { MangaId = t2.Id, CurrentPage = 0 });
        await repo.SaveAsync();

        var scanner = new LibraryScanner(repo);
        Assert.Equal(AppPaths.LibraryDir, scanner.Root);
        Assert.True((await scanner.ScanAsync()).Ok);

        Assert.Equal(AppPaths.LibraryDir, store.LibraryRoot);
        Assert.Equal([t1.Id, t2.Id], repo.Current.Mangas.Select(m => m.Id));
        Assert.Equal("Berserk/Tomo 1.cbz", repo.Current.Mangas[0].RelativePath);
        Assert.True(repo.Current.Mangas[0].IsFavorite);
        Assert.Equal(2, repo.Current.Progress.Count);
        Assert.Equal(1, repo.Current.Progress.Single(p => p.MangaId == t1.Id).CurrentPage);
        var cols = repo.Current.Collections;
        Assert.Equal([berserk.Id, rezero.Id], cols.Select(c => c.Id));
        Assert.True(cols[0].IsFavorite);
        Assert.Equal("Guts", cols[0].Description);
        Assert.Equal("Re_Zero", cols[1].Name); // ahora se llama como su carpeta
        Assert.Equal([t2.Id], cols[1].MangaIds); // el que estaba fuera de la carpeta se cae
    }

    [Fact]
    public async Task Without_a_folder_it_asks_for_one_and_changes_nothing()
    {
        using var tmp = new TempDataDir();
        var repo = await NewRepoAsync();
        repo.Current.Mangas.Add(new Manga { Title = "Antiguo", FilePath = "/no/existe.cbz" });

        var scanner = new LibraryScanner(repo);
        Assert.Null(scanner.Root);
        var result = await scanner.ScanAsync();

        Assert.Equal(ScanStatus.NoRoot, result.Status);
        Assert.NotNull(result.Message);
        Assert.Single(repo.Current.Mangas);
    }

    // Un disco externo sin conectar no puede borrar el progreso de toda la biblioteca.
    [Fact]
    public async Task Unreadable_folder_gives_a_message_and_keeps_the_data()
    {
        using var tmp = new TempDataDir();
        var root = Path.Combine(tmp.Root, "Mis mangas");
        Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 1.cbz", "1.png");
        var repo = await NewRepoAsync(root);
        var scanner = new LibraryScanner(repo);
        await scanner.ScanAsync();
        repo.Current.Progress.Add(new ReadingProgress { MangaId = repo.Current.Mangas[0].Id, CurrentPage = 0 });

        Directory.Move(root, root + " (desconectado)");
        var result = await scanner.ScanAsync();

        Assert.Equal(ScanStatus.Unreadable, result.Status);
        Assert.Contains(root, result.Message);
        Assert.Single(repo.Current.Mangas);
        Assert.Single(repo.Current.Collections);
        Assert.Single(repo.Current.Progress);
    }

    [Fact]
    public async Task Choosing_another_folder_with_the_same_layout_keeps_progress()
    {
        using var tmp = new TempDataDir();
        var root = Path.Combine(tmp.Root, "Mis mangas");
        Fixtures.MakeCbz(Path.Combine(root, "Berserk"), "Tomo 1.cbz", "1.png");
        var repo = await NewRepoAsync(root);
        var scanner = new LibraryScanner(repo);
        await scanner.ScanAsync();
        var id = repo.Current.Mangas[0].Id;

        var moved = Path.Combine(tmp.Root, "Disco nuevo", "Mangas");
        Fixtures.MakeCbz(Path.Combine(moved, "Berserk"), "Tomo 1.cbz", "1.png");
        Assert.True((await scanner.SetRootAsync(moved)).Ok);

        Assert.Equal(moved, repo.Current.LibraryRoot);
        var m = Assert.Single(repo.Current.Mangas);
        Assert.Equal(id, m.Id);
        Assert.Equal(Path.Combine(moved, "Berserk", "Tomo 1.cbz"), m.FilePath);
    }

    [Fact]
    public void Natural_order_compares_numbers_by_value()
    {
        string[] names = ["Tomo 10", "tomo 2", "Tomo 1", "Tomo 02b", "Extra", "Tomo 1.5"];
        Assert.Equal(["Extra", "Tomo 1", "Tomo 1.5", "tomo 2", "Tomo 02b", "Tomo 10"],
                     names.Order(NaturalComparer.Instance));
    }
}
