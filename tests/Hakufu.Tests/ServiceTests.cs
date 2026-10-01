using System.IO.Compression;
using Avalonia.Headless.XUnit;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.Tests;

public class DataRepositoryTests
{
    // Review Focus #1: un data.json de la 0.9.7 (con campos online que ya no
    // existen y la clave "friends" en personalización) carga sin perder nada.
    [Fact]
    public async Task Loads_a_0_9_7_data_file()
    {
        using var tmp = new TempDataDir();
        Directory.CreateDirectory(tmp.DataDir);
        var mangaId = Guid.NewGuid();
        await File.WriteAllTextAsync(AppPaths.DataFile, $$"""
        {
          "Mangas": [ { "Id": "{{mangaId}}", "Title": "Tomo 1", "FilePath": "C:\\m\\t1.cbz",
                        "CoverCachePath": "", "TotalPages": 20,
                        "CloudinaryCoverUrl": "https://res.cloudinary.com/x.png",
                        "CloudPath": "/Hakufu/t1.cbz", "IsFavorite": true } ],
          "Collections": [ { "Name": "Berserk", "MangaIds": [ "{{mangaId}}" ] } ],
          "Favorites": [ { "SlotIndex": 0 } ],
          "ActiveTheme": "Dark",
          "Customization": { "NavIcons": { "account": { "Path": "b.png", "Opacity": 0.5 } } }
        }
        """);

        var repo = new JsonDataRepository();
        await repo.LoadAsync();

        var manga = Assert.Single(repo.Current.Mangas);
        Assert.Equal("Tomo 1", manga.Title);
        Assert.True(manga.IsFavorite);
        Assert.Equal("Dark", repo.Current.ActiveTheme);
        // Lo que ya no existe (Personalizar, huecos de favoritos) se ignora sin romper.
        Assert.Equal("", repo.Current.Profile.Name);
        Assert.Empty(repo.Current.ReadingLog);
    }

    // App carga y guarda bloqueando el hilo de UI (antes de mostrar la ventana y
    // al salir). Con alguna continuación que volviera a ese hilo, la app se
    // colgaba al arrancar (con data.json grande) o no terminaba al cerrar (con
    // data.json pequeño). El Wait con límite hace que el test falle en vez de colgarse.
    [AvaloniaFact]
    public void Load_and_save_can_block_the_ui_thread_without_deadlock()
    {
        foreach (var mangas in new[] { 0, 3, 200 })
        {
            using var tmp = new TempDataDir();
            var repo = new JsonDataRepository();
            for (var i = 0; i < mangas; i++)
                repo.Current.Mangas.Add(new Manga { Title = $"Tomo {i}", FilePath = $"/m/{i}.cbz" });

            Assert.True(repo.SaveAsync().Wait(TimeSpan.FromSeconds(10)), $"SaveAsync bloqueado ({mangas} mangas)");
            var again = new JsonDataRepository();
            Assert.True(again.LoadAsync().Wait(TimeSpan.FromSeconds(10)), $"LoadAsync bloqueado ({mangas} mangas)");
            Assert.Equal(mangas, again.Current.Mangas.Count);
        }
    }

    [Fact]
    public async Task Corrupt_data_file_starts_empty_instead_of_crashing()
    {
        using var tmp = new TempDataDir();
        Directory.CreateDirectory(tmp.DataDir);
        await File.WriteAllTextAsync(AppPaths.DataFile, "{ esto no es json");

        var repo = new JsonDataRepository();
        await repo.LoadAsync();

        Assert.Empty(repo.Current.Mangas);
    }
}

public class PageLoaderTests
{
    // Review Focus #4: se mantiene el orden ordinal de siempre (10 antes que 2),
    // y las imágenes dentro de subcarpetas cuentan como páginas.
    [AvaloniaFact]
    public async Task Cbz_pages_keep_ordinal_order_and_decode()
    {
        using var tmp = new TempDataDir();
        var cbz = Fixtures.MakeCbz(Path.Combine(tmp.Root, "Ataque a los Titanes"), "Tomo 1.cbz",
            "2.png", "10.png", "sub/1.png", "notas.txt");

        using var loader = new PageLoaderService(new Manga { FilePath = cbz });

        Assert.Equal(3, loader.TotalPages);
        var page = await loader.LoadPageAsync(0);
        Assert.NotNull(page);
        Assert.Equal(1, page!.PixelSize.Width);
        Assert.Null(await loader.LoadPageAsync(3));
    }

    // Review Focus #2: un manga borrado o movido no tumba el lector.
    [AvaloniaFact]
    public async Task Missing_file_opens_with_zero_pages()
    {
        using var loader = new PageLoaderService(new Manga { FilePath = "/no/existe/tomo.cbz" });

        Assert.Equal(0, loader.TotalPages);
        Assert.Null(await loader.LoadPageAsync(0));
    }

    [AvaloniaFact]
    public async Task Corrupt_archive_opens_with_zero_pages()
    {
        using var tmp = new TempDataDir();
        var bad = Path.Combine(tmp.Root, "roto.cbz");
        await File.WriteAllTextAsync(bad, "no soy un zip");

        using var loader = new PageLoaderService(new Manga { FilePath = bad });

        Assert.Equal(0, loader.TotalPages);
    }

    // pdfium nativo: este test es el que dice en CI si Docnet funciona en cada SO/arquitectura.
    [AvaloniaFact]
    public async Task Pdf_page_renders_with_native_pdfium()
    {
        using var tmp = new TempDataDir();
        var pdf = Fixtures.MakePdf(tmp.Root, "Tomo con tilde é.pdf");

        using var loader = new PageLoaderService(new Manga { FilePath = pdf });

        Assert.Equal(1, loader.TotalPages);
        var page = await loader.LoadPageAsync(0);
        Assert.NotNull(page);
        Assert.True(page!.PixelSize.Width > 0);
    }
}

public class CoverServiceTests
{
    [AvaloniaFact]
    public async Task Extracts_and_caches_first_image_of_a_cbz()
    {
        using var tmp = new TempDataDir();
        var cbz = Fixtures.MakeCbz(tmp.Root, "Tomo 1.cbz", "b.png", "a.png");
        var id = Guid.NewGuid();

        var path = await new CoverService().ExtractAndCacheCoverAsync(cbz, id);

        Assert.Equal(Path.Combine(AppPaths.CoversDir, $"{id}.png"), path);
        Assert.True(File.Exists(path));
    }

    [AvaloniaFact]
    public async Task Pdf_cover_is_cached()
    {
        using var tmp = new TempDataDir();
        var pdf = Fixtures.MakePdf(tmp.Root, "Tomo 1.pdf");

        var path = await new CoverService().ExtractAndCacheCoverAsync(pdf, Guid.NewGuid());

        Assert.True(File.Exists(path));
    }

    // Review Focus #2.
    [AvaloniaFact]
    public async Task Missing_file_gives_no_cover()
    {
        using var tmp = new TempDataDir();
        var svc = new CoverService();

        Assert.Equal("", await svc.ExtractAndCacheCoverAsync("/no/existe.cbz", Guid.NewGuid()));
        Assert.Null(await svc.GetCoverAsync(new Manga { FilePath = "/no/existe.cbz" }));
    }
}

public class BackupServiceTests
{
    /// <summary>Biblioteca con las colecciones dadas (un tomo cada una) en su carpeta, ya leída.</summary>
    private static async Task<(JsonDataRepository repo, string library)> MachineWith(string library, params string[] collections)
    {
        foreach (var c in collections) Fixtures.MakeCbz(Path.Combine(library, c), "Tomo 1.cbz", "1.png");
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        repo.Current.LibraryRoot = library;
        Assert.True((await new LibraryScanner(repo).ScanAsync()).Ok);
        return (repo, library);
    }

    private static Manga Tomo(JsonDataRepository repo, string collection) =>
        repo.Current.Mangas.Single(m => m.RelativePath == $"{collection}/Tomo 1.cbz");

    private static int? Page(JsonDataRepository repo, string collection) =>
        repo.Current.Progress.FirstOrDefault(p => p.MangaId == Tomo(repo, collection).Id)?.CurrentPage;

    [Fact]
    public async Task Export_chosen_collections_then_merge_into_another_machine()
    {
        using var tmp = new TempDataDir();
        var (repo, _) = await MachineWith(Path.Combine(tmp.Root, "Mangas de origen"), "Berserk", "Vagabond");
        Directory.CreateDirectory(AppPaths.ProfileDir);
        var avatar = Path.Combine(AppPaths.ProfileDir, "avatar.png");
        await File.WriteAllBytesAsync(avatar, Fixtures.TinyPng);
        repo.Current.Profile.Name = "Dani";
        repo.Current.Profile.AvatarPath = avatar;
        repo.Current.Progress.Add(new ReadingProgress { MangaId = Tomo(repo, "Berserk").Id, CurrentPage = 5 });
        repo.Current.Progress.Add(new ReadingProgress { MangaId = Tomo(repo, "Vagabond").Id, CurrentPage = 9 });
        Tomo(repo, "Berserk").IsFavorite = true;

        // Solo Berserk.
        var berserk = repo.Current.Collections.Single(c => c.Name == "Berserk").Id;
        var zip = Path.Combine(tmp.Root, "copia.zip");
        await new BackupService(repo).ExportAsync(zip, new BackupOptions([berserk]));
        using (var archive = ZipFile.OpenRead(zip))
        {
            Assert.DoesNotContain(archive.Entries, e => e.FullName.EndsWith(".cbz"));
            Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith("covers/"));
            Assert.Contains(archive.Entries, e => e.FullName == "profile/avatar.png");
        }

        // "Otro equipo" con las dos series; Vagabond ya iba por la página 2 allí.
        var otherDir = Path.Combine(tmp.Root, "otro equipo");
        Environment.SetEnvironmentVariable("HAKUFU_DATA_DIR", otherDir);
        var (repo2, otherLibrary) = await MachineWith(Path.Combine(tmp.Root, "Mangas de destino"), "Berserk", "Vagabond");
        repo2.Current.Progress.Add(new ReadingProgress { MangaId = Tomo(repo2, "Vagabond").Id, CurrentPage = 2 });

        var result = await new BackupService(repo2).ImportAsync(zip);
        Assert.True(result.Ok);
        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Missing);
        Assert.Equal(otherLibrary, repo2.Current.LibraryRoot);
        Assert.Equal(5, Page(repo2, "Berserk"));
        Assert.True(Tomo(repo2, "Berserk").IsFavorite);
        Assert.Equal(2, Page(repo2, "Vagabond"));            // no venía en la copia: no se toca
        Assert.Equal("Dani", repo2.Current.Profile.Name);
        Assert.Equal(Path.Combine(otherDir, "profile", "avatar.png"), repo2.Current.Profile.AvatarPath);
        Assert.True(File.Exists(repo2.Current.Profile.AvatarPath));
    }

    [Fact]
    public async Task Profile_only_copy_keeps_all_reading_progress_untouched()
    {
        using var tmp = new TempDataDir();
        var (repo, _) = await MachineWith(Path.Combine(tmp.Root, "Mangas"), "Berserk");
        repo.Current.Profile.Name = "Dani";
        repo.Current.Progress.Add(new ReadingProgress { MangaId = Tomo(repo, "Berserk").Id, CurrentPage = 7 });

        var zip = Path.Combine(tmp.Root, "perfil.zip");
        await new BackupService(repo).ExportAsync(zip, BackupOptions.ProfileOnly);

        repo.Current.Profile.Name = "Otro nombre";
        var result = await new BackupService(repo).ImportAsync(zip);
        Assert.True(result.Ok);
        Assert.Equal(0, result.Applied);
        Assert.Equal("Dani", repo.Current.Profile.Name);
        Assert.Equal(7, Page(repo, "Berserk"));
        Assert.Single(repo.Current.Collections);
    }

    [Fact]
    public async Task Old_full_backups_still_replace_the_library()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        repo.Current.Mangas.Add(new Manga { Title = "Lo de ahora" });

        var zip = Path.Combine(tmp.Root, "antigua.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var s = new StreamWriter(archive.CreateEntry("hakufu-backup.json").Open()))
                await s.WriteAsync("""{ "Version": 1, "DataDir": "C:\\x", "CreatedAt": "2026-09-01T00:00:00" }""");
            using (var s = new StreamWriter(archive.CreateEntry("data.json").Open()))
                await s.WriteAsync("""{ "Mangas": [ { "Title": "De la copia" } ] }""");
        }

        var result = await new BackupService(repo).ImportAsync(zip);
        Assert.True(result.Ok);
        Assert.True(result.Legacy);
        Assert.Equal("De la copia", Assert.Single(repo.Current.Mangas).Title);
    }

    // Review Focus #3: un zip que no es una copia de Hakufu no toca la biblioteca.
    [Fact]
    public async Task Import_of_a_foreign_zip_changes_nothing()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        repo.Current.Mangas.Add(new Manga { Title = "Mío" });

        var foreign = Fixtures.MakeCbz(tmp.Root, "otra cosa.zip", "foto.png");
        var notZip  = Path.Combine(tmp.Root, "texto.zip");
        await File.WriteAllTextAsync(notZip, "hola");

        // Zip de otra app que casualmente trae un data.json (sin manifiesto de Hakufu).
        var otherApp = Path.Combine(tmp.Root, "otra-app.zip");
        using (var archive = ZipFile.Open(otherApp, ZipArchiveMode.Create))
        using (var s = new StreamWriter(archive.CreateEntry("data.json").Open()))
            await s.WriteAsync("""{ "version": 3, "items": [] }""");

        var svc = new BackupService(repo);
        Assert.False((await svc.ImportAsync(otherApp)).Ok);
        Assert.False((await svc.ImportAsync(foreign)).Ok);
        Assert.False((await svc.ImportAsync(notZip)).Ok);
        Assert.False((await svc.ImportAsync(Path.Combine(tmp.Root, "no existe.zip"))).Ok);
        Assert.Equal("Mío", Assert.Single(repo.Current.Mangas).Title);
    }

    [Fact]
    public async Task Import_ignores_entries_that_escape_the_data_folder()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();

        var zip = Path.Combine(tmp.Root, "malicioso.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var s = new StreamWriter(archive.CreateEntry("hakufu-backup.json").Open()))
                await s.WriteAsync("""{ "Version": 1, "DataDir": "C:\\x", "CreatedAt": "2026-10-01T00:00:00" }""");
            using (var s = new StreamWriter(archive.CreateEntry("data.json").Open()))
                await s.WriteAsync("{}");
            using (var s = new StreamWriter(archive.CreateEntry("covers/../../fuera.txt").Open()))
                await s.WriteAsync("x");
        }

        Assert.True((await new BackupService(repo).ImportAsync(zip)).Ok);
        Assert.False(File.Exists(Path.Combine(tmp.Root, "fuera.txt")));
    }

    // Regresión (01/10/2026): un guardado en segundo plano que terminaba después
    // de que el test devolviera HAKUFU_DATA_DIR a su valor escribía los datos de
    // prueba en la biblioteca real. El repositorio guarda siempre donde cargó.
    [Fact]
    public async Task Repository_saves_where_it_loaded_even_if_the_data_dir_changes()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        repo.Current.Profile.Name = "prueba";

        var elsewhere = Path.Combine(tmp.Root, "otra carpeta");
        Environment.SetEnvironmentVariable("HAKUFU_DATA_DIR", elsewhere);
        await repo.SaveAsync();

        Assert.True(File.Exists(Path.Combine(tmp.DataDir, "data.json")));
        Assert.False(File.Exists(Path.Combine(elsewhere, "data.json")));
    }

    [Fact]
    public void Tests_never_point_at_the_real_data_folder()
    {
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu");
        Assert.NotEqual(real, AppPaths.DataDir);
    }
}
