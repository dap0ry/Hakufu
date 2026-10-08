using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Hakufu.I18n;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>La guía de Ayuda en inglés: título, secciones, índice y preguntas frecuentes.</summary>
public class I18nHelpTests
{
    [AvaloniaFact]
    public void Help_guide_shows_in_english()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<HelpViewModel>();
            var view = app.AssertShows<HelpView>();
            var vm = Assert.IsType<HelpViewModel>(app.Root.Navigation.CurrentViewModel);

            var texts = view.GetVisualDescendants().OfType<TextBlock>()
                            .Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)).ToList();
            Assert.Contains("Getting started", texts);
            Assert.Contains("Frequently asked questions", texts);
            Assert.Contains("Keyboard shortcuts", texts);
            Assert.DoesNotContain("Empezar", texts);

            Assert.Equal("Library and collections", vm.Sections[1].Title);
            Assert.Equal("Does Hakufu copy my manga?", vm.Questions[0].Question);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [AvaloniaFact]
    public void Help_guide_stays_in_spanish_by_default()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<HelpViewModel>();
        var view = app.AssertShows<HelpView>();
        var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Empezar", texts);
        Assert.Contains("Preguntas frecuentes", texts);
    }
}
