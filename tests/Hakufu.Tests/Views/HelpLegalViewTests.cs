using Avalonia.Headless.XUnit;
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
}
