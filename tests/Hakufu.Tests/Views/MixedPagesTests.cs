using System.Diagnostics;
using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>Doble página con hojas verticales y apaisadas mezcladas: siempre dos juntas y sin animación.</summary>
public class MixedPagesTests
{
    private static string MakeBook(string dir, string pattern)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Libro.cbz");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        for (var i = 0; i < pattern.Length; i++)
        {
            using var s = zip.CreateEntry($"{i:000}.png").Open();
            s.Write(pattern[i] == 'W' ? Fixtures.WidePng : Fixtures.TallPng);
        }
        return path;
    }

    [AvaloniaFact]
    public void Two_page_mode_always_pairs_and_mixed_shapes_change_without_animation()
    {
        using var app = ViewSmoke.Start();
        app.Root.Repo.Current.Reader.TwoPageByDefault = true;
        var manga = new Manga { Title = "Libro", FilePath = MakeBook(Path.Combine(Path.GetTempPath(), $"mixto {Guid.NewGuid():N}"), "VVVWVV"), TotalPages = 6 };

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(manga, 0));
        app.AssertShows<ReaderView>();
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        var flipLayer = app.Window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "FlipLayer");

        void Wait(Func<bool> ok)
        {
            var sw = Stopwatch.StartNew();
            while (!ok() && sw.ElapsedMilliseconds < 5000) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); app.Pump(); }
            Assert.True(ok());
        }

        Wait(() => vm.PageRight is not null);
        Assert.True(vm.ShowsTwoPages);

        // 0-1 → 2-3: la 3 es apaisada, pero se ven las dos juntas, como antes.
        vm.NextPageCommand.Execute(null);
        Wait(() => vm.CurrentPage == 2 && ReaderViewModel.IsWide(vm.PageRight));
        Assert.True(vm.ShowsTwoPages);
        Assert.NotNull(vm.PageLeft);
        // Y sin animación: en cuanto están cargadas no queda nada girando.
        Wait(() => flipLayer.Children.Count == 0);
    }
}
