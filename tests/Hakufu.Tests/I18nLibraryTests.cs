using Avalonia.Controls;
using Avalonia.VisualTree;
using Hakufu.I18n;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Biblioteca y colecciones en inglés: textos de la vista y de los ViewModels (con números).</summary>
public class I18nLibraryTests
{
    private static List<string?> VisibleTexts(Control view)
        => view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Library_in_english()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<LibraryViewModel>();
            var view = app.AssertShows<Hakufu.MVVM.View.LibraryView>();

            var texts = VisibleTexts(view);
            Assert.Contains("Library", texts);
            Assert.Contains("1 collection · 2 volumes", texts);
            Assert.Contains("2 volumes", texts);
            var buttons = view.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible)
                              .Select(b => b.Content as string).ToList();
            Assert.Contains("Refresh", buttons);
            Assert.Contains("Open folder", buttons);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Collection_in_english()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<CollectionDetailViewModel>(app.SampleCollection.Id);
            var view = app.AssertShows<Hakufu.MVVM.View.CollectionDetailView>();

            var texts = VisibleTexts(view);
            // "Tomo 1" está en la última página (índice 2 de 3): terminado; el otro, sin empezar.
            Assert.Contains("2 volumes · 1 finished", texts);
            Assert.Contains("Finished", texts);
            Assert.Contains("2 pages", texts);
            Assert.Contains("Sort", texts);

            var buttons = view.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string).ToList();
            Assert.Contains("← Library", buttons);
            Assert.Contains("Custom", buttons);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Loose_volumes_and_scan_errors_in_english_keep_the_stored_name()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            var loose = new Collection { Name = LibraryScanner.LooseCollectionName, RelativePath = "" };
            Assert.Equal("No collection", new CollectionCardViewModel(loose).Name);
            Assert.Equal("Sin colección", loose.Name); // el dato guardado no cambia
            Assert.Equal("Berserk", new CollectionCardViewModel(new Collection { Name = "Berserk", RelativePath = "Berserk" }).Name);

            var msg = new ScanResult(ScanStatus.Unreadable, "/Volumes/USB/Manga").Message;
            Assert.Equal("Can't read the folder “/Volumes/USB/Manga”. Is the drive connected, or has it moved?", msg);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }
}
