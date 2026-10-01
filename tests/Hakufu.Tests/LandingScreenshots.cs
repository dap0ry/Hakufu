using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>
/// No es un test: genera las capturas de la landing (web/img/app-*.png) con la
/// app real renderizada sin pantalla. Solo hace algo si se le pasa una carpeta
/// con mangas de ejemplo (una subcarpeta .cbz por colección):
///
///   HAKUFU_SCREENSHOTS_SRC=/ruta/demo HAKUFU_SCREENSHOTS_OUT=web/img \
///     dotnet test tests/Hakufu.Tests --filter LandingScreenshots
/// </summary>
public class LandingScreenshots
{
    [AvaloniaFact]
    public void Capture()
    {
        var src = Environment.GetEnvironmentVariable("HAKUFU_SCREENSHOTS_SRC");
        var outDir = Environment.GetEnvironmentVariable("HAKUFU_SCREENSHOTS_OUT");
        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);

        foreach (var theme in new[] { "Dark", "Light" })
        {
            using var tmp = new TempDataDir();
            var repo = new JsonDataRepository();
            repo.LoadAsync().GetAwaiter().GetResult();
            var store = repo.Current;
            var rnd = new Random(7);
            var day = DateTime.Now;

            foreach (var dir in Directory.GetDirectories(src).Order())
            {
                var col = new Collection { Name = Path.GetFileName(dir), CreatedAt = day };
                foreach (var file in Directory.GetFiles(dir, "*.cbz").Order())
                {
                    var manga = new Manga
                    {
                        Title = Path.GetFileNameWithoutExtension(file), FilePath = file,
                        TotalPages = 7, DateAdded = day,
                    };
                    store.Mangas.Add(manga);
                    col.MangaIds.Add(manga.Id);
                    store.Progress.Add(new ReadingProgress
                    {
                        MangaId = manga.Id, CurrentPage = rnd.Next(2, 7), LastRead = day.AddHours(-rnd.Next(1, 90)),
                    });
                    day = day.AddMinutes(-37);
                }
                store.Collections.Add(col);
            }

            store.Collections[0].IsFavorite = true;
            store.Collections[3].IsFavorite = true;
            for (var i = 0; i < 3; i++)
            {
                store.Mangas[i * 3].IsFavorite = true;
                store.Favorites[i].MangaId = store.Mangas[i * 3].Id;
            }
            foreach (var (m, hoursAgo) in store.Mangas.Take(5).Select((m, i) => (m, i * 19 + 2)))
                store.History.Add(new ReadingHistoryEntry { MangaId = m.Id, CompletedAt = DateTime.Now.AddHours(-hoursAgo) });
            store.TotalUsageSeconds = 41 * 3600 + 25 * 60;
            store.ActiveTheme = theme;

            var root = new CompositionRoot(repo);
            root.ApplySavedAppearance();
            var window = new MainWindow { DataContext = root.CreateMainViewModel(), Width = 1280, Height = 800 };
            window.Show();

            void Shot(string name)
            {
                for (var i = 0; i < 40; i++)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(25);
                }
                window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, $"app-{name}-{theme.ToLowerInvariant()}.png"));
            }

            root.Navigation.NavigateTo<HomeViewModel>();
            Shot("home");
            root.Navigation.NavigateTo<LibraryViewModel>();
            Shot("library");
            root.Navigation.NavigateTo<CollectionDetailViewModel>(store.Collections[0].Id);
            Shot("collection");
            root.Navigation.NavigateTo<ProfileViewModel>();
            Shot("profile");
            root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(store.Mangas[0], 2));
            Shot("reader");

            window.Close();
        }
    }
}
