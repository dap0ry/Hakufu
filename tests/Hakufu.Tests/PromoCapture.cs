using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>
/// No es un test: genera los fotogramas de la app real para el vídeo de promo
/// (promo/public/tour). Solo hace algo si se le pasa una biblioteca de ejemplo:
///
///   python3 scripts/landing-demo-mangas.py /tmp/demo-promo promo
///   HAKUFU_PROMO_SRC=/tmp/demo-promo HAKUFU_PROMO_OUT=promo/public/tour \
///     dotnet test tests/Hakufu.Tests --filter PromoCapture
/// </summary>
public class PromoCapture
{
    private const int ScrollFrames = 60;

    [AvaloniaFact]
    public void Capture()
    {
        var src = Environment.GetEnvironmentVariable("HAKUFU_PROMO_SRC");
        var outDir = Environment.GetEnvironmentVariable("HAKUFU_PROMO_OUT");
        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);

        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        repo.LoadAsync().GetAwaiter().GetResult();
        var store = repo.Current;
        var rnd = new Random(11);
        var day = DateTime.Now;
        store.LibraryRoot = src;
        store.ActiveTheme = "Dark";
        store.Reader.PageTurnAnimation = false;

        foreach (var dir in Directory.GetDirectories(src).Order())
        {
            var name = Path.GetFileName(dir);
            var col = new Collection { Name = name, RelativePath = name, CreatedAt = day };
            foreach (var file in Directory.GetFiles(dir, "*.cbz").Order(StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CompareOptions.NumericOrdering)))
            {
                var manga = new Manga
                {
                    Title = Path.GetFileNameWithoutExtension(file), FilePath = file,
                    RelativePath = $"{name}/{Path.GetFileName(file)}", TotalPages = 7, DateAdded = day,
                };
                store.Mangas.Add(manga);
                col.MangaIds.Add(manga.Id);
                store.Progress.Add(new ReadingProgress
                {
                    MangaId = manga.Id, CurrentPage = rnd.Next(1, 7), LastRead = day.AddHours(-rnd.Next(1, 300)),
                });
                day = day.AddMinutes(-23);
            }
            store.Collections.Add(col);
        }

        foreach (var i in new[] { 0, 3, 7 }) store.Collections[i].IsFavorite = true;
        store.Profile.Name = "Dani";
        store.Profile.FavoriteMangaId = store.Mangas[2].Id;
        store.Profile.MemberSince = new DateTime(2026, 8, 17);
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var d = 0; d < 84; d++)
            if (rnd.NextDouble() < 0.7)
                store.ReadingLog.Add(new ReadingDay { Date = today.AddDays(-d), Seconds = rnd.Next(20, 140) * 60, Pages = rnd.Next(30, 180) });
        foreach (var (m, h) in store.Mangas.Take(9).Select((m, i) => (m, i * 13 + 2)))
            store.History.Add(new ReadingHistoryEntry { MangaId = m.Id, CompletedAt = DateTime.Now.AddHours(-h) });
        store.TotalUsageSeconds = 128 * 3600;

        var root = new CompositionRoot(repo);
        root.ApplySavedAppearance();
        var window = new MainWindow { DataContext = root.CreateMainViewModel(), Width = 1728, Height = 1080 };
        window.Show();

        void Settle(int jobs, int sleep)
        {
            for (var i = 0; i < jobs; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(sleep);
            }
        }
        void Save(string name) => window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, name + ".png"));

        // Baja por la pantalla con aceleración y frenada, un PNG por fotograma.
        void Scroll(string name)
        {
            var sv = window.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Extent.Height > s.Viewport.Height);
            var max = sv.Extent.Height - sv.Viewport.Height;
            // Primera pasada para que carguen todas las portadas.
            for (var y = 0.0; y <= max; y += sv.Viewport.Height / 2)
            {
                sv.Offset = new Vector(0, y);
                Settle(20, 25);
            }
            sv.Offset = new Vector(0, max);
            Settle(30, 25);
            Directory.CreateDirectory(Path.Combine(outDir, name));
            for (var f = 0; f < ScrollFrames; f++)
            {
                var t = (double)f / (ScrollFrames - 1);
                var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
                sv.Offset = new Vector(0, max * e);
                Settle(3, 5);
                Save(Path.Combine(name, $"{f:000}"));
            }
        }

        root.Navigation.NavigateTo<HomeViewModel>();
        Settle(40, 25);
        Save("home");

        root.Navigation.NavigateTo<LibraryViewModel>();
        Settle(60, 25);
        Save("library");
        Scroll("library-scroll");

        var big = store.Collections.First(c => c.MangaIds.Count >= 20);
        root.Navigation.NavigateTo<CollectionDetailViewModel>(big.Id);
        Settle(60, 25);
        Save("collection");
        Scroll("collection-scroll");

        root.Navigation.NavigateTo<ProfileViewModel>();
        Settle(60, 25);
        Save("profile");

        // Lector: una página y luego dos, una captura por página.
        var first = store.Mangas.First(m => m.Id == big.MangaIds[0]);
        root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(first, 0));
        Settle(60, 25);
        var reader = (ReaderViewModel)root.Navigation.CurrentViewModel!;
        reader.SinglePageCommand.Execute(null);
        for (var p = 0; p < 5; p++)
        {
            Settle(40, 25);
            Save($"reader-1p-{p}");
            reader.NextPageCommand.Execute(null);
        }
        root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(first, 1));
        Settle(40, 25);
        reader = (ReaderViewModel)root.Navigation.CurrentViewModel!;
        reader.TwoPagesCommand.Execute(null);
        for (var p = 0; p < 3; p++)
        {
            Settle(40, 25);
            Save($"reader-2p-{p}");
            reader.NextPageCommand.Execute(null);
        }

        window.Close();
    }
}
