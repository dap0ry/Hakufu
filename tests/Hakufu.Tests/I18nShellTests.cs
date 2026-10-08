using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Hakufu.I18n;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Inicio, ventana, lector y actualizaciones en inglés.</summary>
public class I18nShellTests
{
    private static List<string> Texts(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();

    [AvaloniaFact]
    public void Home_is_in_English_with_the_page_count()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            var home = app.AssertShows<HomeView>();
            var texts = Texts(home);
            Assert.Contains("Library", texts);
            Assert.Contains("Continue reading", texts);
            Assert.Contains("Page 3 of 3", texts);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [AvaloniaFact]
    public void Update_banner_speaks_English_and_follows_language_changes()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            var svc = new FakeUpdateService { Result = new(UpdateCheckStatus.Available, "0.11.0", true) };
            var vm = new UpdateBannerViewModel(svc, new UpdateSettings { CheckOnStartup = true },
                                               () => { }, _ => { }, TimeSpan.Zero);
            vm.StartAsync().GetAwaiter().GetResult();
            Assert.Equal("Hakufu 0.11.0 available", vm.Message);
            Assert.Equal("Update", vm.PrimaryText);
            Assert.Equal("Version 0.10.1", vm.CurrentVersion);

            svc.Result = new(UpdateCheckStatus.UpToDate);
            vm.CheckNowAsync().GetAwaiter().GetResult();
            Assert.Equal("You have the latest version.", vm.CheckStatus);

            Localizer.Instance.SetLanguage("es");
            Assert.Equal("Ya tienes la última versión.", vm.CheckStatus);
            Assert.Equal("Hakufu 0.11.0 disponible", vm.Message);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [AvaloniaFact]
    public void Page_header_back_text_defaults_to_home_in_the_current_language()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            var byDefault = new Hakufu.MVVM.View.Controls.PageHeader();
            var custom = new Hakufu.MVVM.View.Controls.PageHeader { BackText = "← Settings" };
            var window = new Window { Content = new StackPanel { Children = { byDefault, custom } } };
            window.Show();
            Assert.Equal("← Home", byDefault.BackText);
            Assert.Equal("← Settings", custom.BackText);

            Localizer.Instance.SetLanguage("es");
            Assert.Equal("← Inicio", byDefault.BackText);
            window.Close();
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [AvaloniaFact]
    public void Window_and_reader_buttons_are_in_English()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Window.OwnTitleBar = true;
            app.Pump();
            Assert.Equal("Full screen", ToolTip.GetTip(app.Window.FullScreenButton));

            app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
            var reader = app.AssertShows<ReaderView>();
            var buttons = reader.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string).ToList();
            Assert.Contains("Close", buttons);
            Assert.Contains("2 pages", buttons);

            Localizer.Instance.SetLanguage("es");
            app.Pump();
            Assert.Equal("Pantalla completa", ToolTip.GetTip(app.Window.FullScreenButton));
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }
}
