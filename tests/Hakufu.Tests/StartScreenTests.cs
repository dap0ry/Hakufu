using Avalonia.Headless.XUnit;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>HAKUFU_START_SCREEN: el CI abre cada pantalla en el simulador de iOS para capturarla.</summary>
public class StartScreenTests
{
    [AvaloniaTheory]
    [InlineData("library",    typeof(LibraryViewModel))]
    [InlineData("collection", typeof(CollectionDetailViewModel))]
    [InlineData("reader",     typeof(ReaderViewModel))]
    [InlineData("settings",   typeof(SettingsViewModel))]
    [InlineData("profile",    typeof(ProfileViewModel))]
    [InlineData("pdf",        typeof(HomeViewModel))]   // la biblioteca de ejemplo no tiene PDF
    [InlineData("",           typeof(HomeViewModel))]
    [InlineData("nada",       typeof(HomeViewModel))]
    public void Opens_the_requested_screen(string screen, Type expected)
    {
        using var app = ViewSmoke.StartMobile(390, 844);

        StartScreen.Apply(app.Root, screen);
        app.Pump();

        Assert.IsType(expected, app.Root.Navigation.CurrentViewModel);
    }

    [AvaloniaFact]
    public void Pdf_opens_the_first_pdf_volume_in_the_reader()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        var pdf = new Hakufu.MVVM.Model.Manga
        {
            Title = "Tomo en PDF", RelativePath = "PDF/Tomo en PDF.pdf",
            FilePath = Fixtures.MakePdf(Path.Combine(Hakufu.Data.AppPaths.FixedLibraryRoot!, "PDF"), "Tomo en PDF.pdf"),
        };
        app.Root.Repo.Current.Mangas.Add(pdf);

        StartScreen.Apply(app.Root, "pdf");
        app.Pump();

        var reader = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        Assert.Equal("Tomo en PDF", reader.MangaTitle);
    }
}
