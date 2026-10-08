using Avalonia.Controls;
using Avalonia.VisualTree;
using Hakufu.MVVM.Model;
using Hakufu.I18n;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>El perfil (y la tarjeta que se exporta) en inglés, con números y fechas en formato inglés.</summary>
public class I18nProfileTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Profile_and_card_show_in_English_with_English_numbers_and_dates()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Repo.Current.Profile.MemberSince = new DateTime(2026, 3, 14);
            app.Root.Repo.Current.ReadingLog.Add(new ReadingDay
                { Date = DateOnly.FromDateTime(DateTime.Now), Seconds = 5400, Pages = 1234 });

            app.Root.Navigation.NavigateTo<ProfileViewModel>();
            var view = app.AssertShows<ProfileView>();
            var vm = Assert.IsType<ProfileViewModel>(app.Root.Navigation.CurrentViewModel);

            Assert.Equal("On Hakufu since March 2026", vm.MemberSinceText);
            Assert.Equal("1,234", vm.PagesText);
            Assert.Equal("1.5", vm.ReadingTimeValue);
            Assert.Equal("This week: 1 h 30 min · 1,234 pages", vm.ThisWeekText);
            Assert.Equal("1-day reading streak", vm.StreakText);
            Assert.Equal("Your name", vm.DisplayName);

            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Volumes finished", texts);
            Assert.Contains("Reading now", texts);
            Assert.DoesNotContain("Tomos terminados", texts);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Profile_stays_byte_identical_in_Spanish()
    {
        Localizer.Instance.SetLanguage("es");
        using var app = ViewSmoke.Start();
        app.Root.Repo.Current.Profile.MemberSince = new DateTime(2026, 3, 14);
        app.Root.Repo.Current.ReadingLog.Add(new ReadingDay
            { Date = DateOnly.FromDateTime(DateTime.Now), Seconds = 5400, Pages = 1234 });

        app.Root.Navigation.NavigateTo<ProfileViewModel>();
        var vm = Assert.IsType<ProfileViewModel>(app.Root.Navigation.CurrentViewModel);

        Assert.Equal("En Hakufu desde marzo de 2026", vm.MemberSinceText);
        Assert.Equal("1.234", vm.PagesText);
        Assert.Equal("1,5", vm.ReadingTimeValue);
        Assert.Equal("Esta semana: 1 h 30 min · 1234 págs.", vm.ThisWeekText);
        Assert.Equal("1 día seguido leyendo", vm.StreakText);
    }
}
