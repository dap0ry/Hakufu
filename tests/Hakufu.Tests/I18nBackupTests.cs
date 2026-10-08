using Avalonia.Controls;
using Avalonia.VisualTree;
using Hakufu.I18n;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Copia de seguridad, aviso legal y diálogos de archivos, en inglés.</summary>
public class I18nBackupTests
{
    private static List<string> Texts(Control view) =>
        view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Backup_screen_in_English()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<BackupViewModel>();
            var view = app.AssertShows<BackupView>();
            var vm = Assert.IsType<BackupViewModel>(app.Root.Navigation.CurrentViewModel);

            Assert.Equal("1 collection · 2 volumes", vm.SummaryText);
            Assert.Equal("Your profile and 1 of 1 collection", vm.ExportSummary);
            Assert.Equal("2 volumes", Assert.Single(vm.CollectionOptions).Detail);
            vm.SelectNoCollectionsCommand.Execute(null);
            Assert.Equal("Only your profile", vm.ExportSummary);

            var texts = Texts(view);
            Assert.Contains("Backup", texts);
            Assert.Contains("Export a backup", texts);
            Assert.Contains("Export backup…", view.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string));

            Assert.Equal("Hakufu backup", FileFilter.Backup.Name);
            Assert.Equal("Manga files", FileFilter.Mangas.Name);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Legal_screen_in_English()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<LegalViewModel>();
            var view = app.AssertShows<LegalView>();
            var vm = Assert.IsType<LegalViewModel>(app.Root.Navigation.CurrentViewModel);

            Assert.Equal(13, vm.LegalSections.Count);
            Assert.Equal("1. Purpose and acceptance of the terms", vm.LegalSections[0].Title);
            Assert.Contains("Daniel Poza Rivera", vm.LegalSections[0].Body);
            Assert.Equal("13. Contact", vm.LegalSections[12].Title);

            var texts = Texts(view);
            Assert.Contains("Legal notice", texts);
            Assert.Contains("← Settings", texts);
            Assert.Contains(texts, t => t.StartsWith("This is a translation; the Spanish version prevails"));
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Backup_and_legal_stay_in_Spanish()
    {
        Localizer.Instance.SetLanguage("es");
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<BackupViewModel>();
        app.AssertShows<BackupView>();
        var vm = Assert.IsType<BackupViewModel>(app.Root.Navigation.CurrentViewModel);
        Assert.Equal("1 colección · 2 tomos", vm.SummaryText);
        Assert.Equal("Copia de Hakufu", FileFilter.Backup.Name);

        app.Root.Navigation.NavigateTo<LegalViewModel>();
        app.AssertShows<LegalView>();
        var legal = Assert.IsType<LegalViewModel>(app.Root.Navigation.CurrentViewModel);
        Assert.Equal("1. Objeto y aceptación de los términos", legal.LegalSections[0].Title);
    }
}
