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
                        "DropboxPath": "/Hakufu/t1.cbz", "IsFavorite": true } ],
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
    [Fact]
    public async Task Export_then_import_restores_library_and_rebases_paths()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();

        // Manga copiado dentro de la biblioteca de Hakufu + portada + foto de perfil.
        Directory.CreateDirectory(AppPaths.LibraryDir);
        Directory.CreateDirectory(AppPaths.CoversDir);
        Directory.CreateDirectory(AppPaths.ProfileDir);
        var mangaPath = Path.Combine(AppPaths.LibraryDir, "Tomo 1.cbz");
        var coverPath = Path.Combine(AppPaths.CoversDir, "c.png");
        var avatar    = Path.Combine(AppPaths.ProfileDir, "avatar.png");
        await File.WriteAllTextAsync(mangaPath, "manga");
        await File.WriteAllBytesAsync(coverPath, Fixtures.TinyPng);
        await File.WriteAllBytesAsync(avatar, Fixtures.TinyPng);
        repo.Current.Mangas.Add(new Manga { Title = "Tomo 1", FilePath = mangaPath, CoverCachePath = coverPath });
        repo.Current.Profile.AvatarPath = avatar;

        var zip = Path.Combine(tmp.Root, "copia.zip");
        await new BackupService(repo).ExportAsync(zip, includeLibraryFiles: true);

        // "Otro equipo": carpeta de datos distinta y vacía.
        var otherDir = Path.Combine(tmp.Root, "otro equipo");
        Environment.SetEnvironmentVariable("HAKUFU_DATA_DIR", otherDir);
        var repo2 = new JsonDataRepository();
        await repo2.LoadAsync();

        Assert.True(await new BackupService(repo2).ImportAsync(zip));

        var m = Assert.Single(repo2.Current.Mangas);
        Assert.Equal(Path.Combine(otherDir, "biblioteca", "Tomo 1.cbz"), m.FilePath);
        Assert.Equal(Path.Combine(otherDir, "covers", "c.png"), m.CoverCachePath);
        Assert.True(File.Exists(m.FilePath));
        Assert.True(File.Exists(m.CoverCachePath));
        Assert.Equal(Path.Combine(otherDir, "profile", "avatar.png"), repo2.Current.Profile.AvatarPath);
        Assert.True(File.Exists(repo2.Current.Profile.AvatarPath));
        Assert.True(File.Exists(AppPaths.DataFile));
    }

    [Fact]
    public async Task Export_without_library_files_leaves_mangas_out()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        Directory.CreateDirectory(AppPaths.LibraryDir);
        await File.WriteAllTextAsync(Path.Combine(AppPaths.LibraryDir, "t.cbz"), "x");

        var zip = Path.Combine(tmp.Root, "copia.zip");
        await new BackupService(repo).ExportAsync(zip, includeLibraryFiles: false);

        using var archive = ZipFile.OpenRead(zip);
        Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith("biblioteca/"));
        Assert.Contains(archive.Entries, e => e.FullName == "data.json");
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
        Assert.False(await svc.ImportAsync(otherApp));
        Assert.False(await svc.ImportAsync(foreign));
        Assert.False(await svc.ImportAsync(notZip));
        Assert.False(await svc.ImportAsync(Path.Combine(tmp.Root, "no existe.zip")));
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

        Assert.True(await new BackupService(repo).ImportAsync(zip));
        Assert.False(File.Exists(Path.Combine(tmp.Root, "fuera.txt")));
    }
}
