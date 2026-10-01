using System.Diagnostics;
using System.IO.Compression;
using Avalonia.Headless.XUnit;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>Doble página: las páginas apaisadas (dobles escaneadas juntas) se ven solas, a lo ancho.</summary>
public class SpreadTests
{
    private static readonly byte[] WidePng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAIAAADwyuo0AAAAEElEQVR4nGM8wYAATEhsBgAT1gDMOSBMTQAAAABJRU5ErkJggg==");

    /// <summary>CBZ con páginas verticales (V) y apaisadas (W) según el patrón, p. ej. "VVWVV".</summary>
    private static string MakeBook(string dir, string pattern)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Libro.cbz");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        for (var i = 0; i < pattern.Length; i++)
        {
            using var s = zip.CreateEntry($"{i:000}.png").Open();
            s.Write(pattern[i] == 'W' ? WidePng : Fixtures.TallPng);
        }
        return path;
    }

    [AvaloniaFact]
    public void Wide_pages_are_shown_alone_and_paging_skips_correctly()
    {
        using var app = ViewSmoke.Start();
        var manga = new Manga { Title = "Libro", FilePath = MakeBook(Path.Combine(Path.GetTempPath(), $"spread {Guid.NewGuid():N}"), "VVVWWVV"), TotalPages = 7 };
        app.Root.Repo.Current.Reader.TwoPageByDefault = true;
        app.Root.Repo.Current.Reader.PageTurnAnimation = false;

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(manga, 0));
        app.AssertShows<ReaderView>();
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);

        var loaded = -1;
        vm.PagesLoaded += (_, page) => loaded = page;

        void Wait(Func<bool> ok)
        {
            var sw = Stopwatch.StartNew();
            while (!ok() && sw.ElapsedMilliseconds < 5000) app.Pump();
            Assert.True(ok());
        }

        Wait(() => vm.PageRight is not null); // primera carga (antes de suscribirse)
        Assert.True(vm.ShowsTwoPages);                    // 0-1: dos verticales

        vm.NextPageCommand.Execute(null);
        Wait(() => vm.CurrentPage == 2 && loaded == 2);
        Assert.False(vm.ShowsTwoPages);                   // 2: la siguiente es apaisada → sola

        vm.NextPageCommand.Execute(null);
        Wait(() => vm.CurrentPage == 3 && loaded == 3);
        Assert.False(vm.ShowsTwoPages);                   // 3: apaisada → sola

        vm.NextPageCommand.Execute(null);
        Wait(() => vm.CurrentPage == 4 && loaded == 4);
        vm.NextPageCommand.Execute(null);
        Wait(() => vm.CurrentPage == 5 && loaded == 5);
        Assert.True(vm.ShowsTwoPages);                    // 5-6: dos verticales otra vez

        // Hacia atrás: de 5 a 4 (apaisada), no a 3.
        vm.PrevPageCommand.Execute(null);
        Wait(() => vm.CurrentPage == 4 && loaded == 4);
        // De 2 a 0: las dos de antes van juntas.
        vm.PrevPageCommand.Execute(null); Wait(() => vm.CurrentPage == 3 && loaded == 3);
        vm.PrevPageCommand.Execute(null); Wait(() => vm.CurrentPage == 2 && loaded == 2);
        vm.PrevPageCommand.Execute(null); Wait(() => vm.CurrentPage == 0);
    }
}
