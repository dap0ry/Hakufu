using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>Humo de Ayuda (guía) y del aviso legal (Ajustes → Acerca de), en tema claro y oscuro.</summary>
public class HelpLegalViewTests
{
    private static readonly bool[] Themes = [false, true];

    [AvaloniaFact]
    public void Settings_button_opens_legal_and_back_returns_to_settings()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<SettingsViewModel>();
            app.AssertShows<SettingsView>();

            var settings = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);
            settings.OpenLegalCommand.Execute(null);
            app.AssertShows<LegalView>();

            var legal = Assert.IsType<LegalViewModel>(app.Root.Navigation.CurrentViewModel);
            Assert.Equal(13, legal.LegalSections.Count);
            Assert.StartsWith("1. Objeto", legal.LegalSections[0].Title);

            legal.GoBackCommand.Execute(null);
            app.AssertShows<SettingsView>();
        }
    }

    [AvaloniaFact]
    public void Help_guide_renders_every_section_with_its_screenshots()
    {
        foreach (var dark in Themes)
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<HelpViewModel>();
            var view = app.AssertShows<HelpView>();
            var vm = Assert.IsType<HelpViewModel>(app.Root.Navigation.CurrentViewModel);

            Assert.Equal(8, vm.Sections.Count);
            foreach (var s in vm.Sections)
            {
                var section = view.FindControl<Control>(s.Id);
                Assert.True(section is { Bounds.Height: > 0 }, $"Falta la sección {s.Id}.");
            }

            // Las capturas vienen de Assets/Help (AvaloniaResource).
            var shots = view.GetVisualDescendants().OfType<Image>().ToList();
            Assert.True(shots.Count >= 7);
            Assert.All(shots, i => Assert.NotNull(i.Source));

            // Atajos: los de fábrica, ya en texto.
            var next = vm.Shortcuts.Single(s => s.Label == "Página siguiente");
            Assert.Equal(["→", "Espacio"], next.Keys);
            Assert.NotEmpty(vm.Questions);
        }
    }

    [AvaloniaFact]
    public void Help_index_scrolls_to_a_section_and_highlights_it()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<HelpViewModel>();
        var view = app.AssertShows<HelpView>();
        var vm = Assert.IsType<HelpViewModel>(app.Root.Navigation.CurrentViewModel);
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "ContentScroll");
        Assert.True(vm.Sections[0].IsActive);

        view.ScrollToSection("SecProfile");
        app.Pump();
        var profile = view.FindControl<Control>("SecProfile")!;
        var top = profile.TranslatePoint(default, scroll)!.Value.Y;
        Assert.True(scroll.Offset.Y > 0);
        Assert.InRange(top, 0, 80); // arriba del todo, con un poco de aire
        Assert.True(vm.Sections.Single(s => s.Id == "SecProfile").IsActive);
        Assert.False(vm.Sections[0].IsActive);

        // El botón del índice hace lo mismo.
        var faqButton = view.GetVisualDescendants().OfType<Button>()
                            .First(b => b.DataContext is HelpSection { Id: "SecFaq" });
        faqButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        app.Pump();
        Assert.True(vm.Sections[^1].IsActive);
    }

    [AvaloniaFact]
    public void Help_buttons_navigate()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<HelpViewModel>();
        app.AssertShows<HelpView>();
        var vm = Assert.IsType<HelpViewModel>(app.Root.Navigation.CurrentViewModel);
        vm.OpenSettingsCommand.Execute(null);
        app.AssertShows<SettingsView>();
    }
}
