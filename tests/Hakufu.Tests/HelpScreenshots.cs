using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>
/// No es un test: genera las capturas de la guía de Ayuda (Assets/Help/*.png)
/// con la app real renderizada sin pantalla, en tema oscuro y con mangas
/// inventados (nunca mangas reales). Solo hace algo si se le pasa la carpeta
/// de mangas de ejemplo (una subcarpeta .cbz por colección):
///
///   python3 scripts/landing-demo-mangas.py /tmp/demo
///   HAKUFU_HELP_SHOTS_SRC=/tmp/demo HAKUFU_HELP_SHOTS_OUT=Assets/Help \
///     dotnet test tests/Hakufu.Tests --filter HelpScreenshots
///
/// Después conviene pasarlas por un optimizador de PNG (pngquant, oxipng o
/// Pillow con optimize=True) para que no pesen de más.
/// </summary>
public class HelpScreenshots
{
    private const int W = 1280, H = 800;

    [AvaloniaFact]
    public void Capture()
    {
        var src    = Environment.GetEnvironmentVariable("HAKUFU_HELP_SHOTS_SRC");
        var outDir = Environment.GetEnvironmentVariable("HAKUFU_HELP_SHOTS_OUT");
        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);

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
            store.Mangas[i * 3].IsFavorite = true;
        store.Profile.Name = "Dani";
        store.Profile.FavoriteMangaId = store.Mangas[3].Id;
        store.Profile.MemberSince = new DateTime(2026, 8, 17);
        // Doce semanas de lectura inventadas para la gráfica.
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var d = 0; d < 84; d++)
            if (rnd.NextDouble() < 0.62)
                store.ReadingLog.Add(new ReadingDay
                {
                    Date = today.AddDays(-d), Seconds = rnd.Next(12, 110) * 60, Pages = rnd.Next(15, 140),
                });
        foreach (var (m, hoursAgo) in store.Mangas.Take(5).Select((m, i) => (m, i * 19 + 2)))
            store.History.Add(new ReadingHistoryEntry { MangaId = m.Id, CompletedAt = DateTime.Now.AddHours(-hoursAgo) });
        store.ActiveTheme = "Dark";
        store.Reader.TwoPageByDefault = true;

        var root = new CompositionRoot(repo);
        root.ApplySavedAppearance();
        var window = new MainWindow { DataContext = root.CreateMainViewModel(), Width = W, Height = H };
        window.Show();

        void Shot(string name, PixelRect? crop = null)
        {
            for (var i = 0; i < 40; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(25);
            }
            var frame = window.CaptureRenderedFrame()!;
            var path  = Path.Combine(outDir, $"{name}.png");
            if (crop is not { } r) { frame.Save(path); return; }
            using var rtb = new RenderTargetBitmap(r.Size, new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
                ctx.DrawImage(frame, new Rect(r.X, r.Y, r.Width, r.Height), new Rect(0, 0, r.Width, r.Height));
            rtb.Save(path);
        }

        // Recortes: solo la parte que se explica en cada sección.
        root.Navigation.NavigateTo<HomeViewModel>();
        Shot("home", new PixelRect(580, 0, 700, H));                       // "Continuar leyendo"
        root.Navigation.NavigateTo<LibraryViewModel>();
        Shot("library", new PixelRect(0, 0, W, 520));
        var kurogane = store.Collections.FirstOrDefault(c => c.Name == "Kurogane") ?? store.Collections[0];
        root.Navigation.NavigateTo<CollectionDetailViewModel>(kurogane.Id);
        Shot("collection", new PixelRect(0, 0, W, 560));
        root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(store.Mangas[0], 1));
        Shot("reader");
        root.Navigation.NavigateTo<ProfileViewModel>();
        Shot("profile");
        root.Navigation.NavigateTo<SettingsViewModel>();
        Shot("settings", new PixelRect((W - 760) / 2, 100, 760, H - 100)); // columna central
        root.Navigation.NavigateTo<BackupViewModel>();
        Shot("backup", new PixelRect(0, 300, 700, H - 300));               // sin la ruta de datos

        window.Close();
    }
}
