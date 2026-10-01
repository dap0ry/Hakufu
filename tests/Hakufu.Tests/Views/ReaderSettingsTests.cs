using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>Ajustes → Lectura: se guardan y el lector los respeta al abrir un manga.</summary>
public class ReaderSettingsTests
{
    private static ReaderViewModel OpenReader(ViewSmoke app)
    {
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.AssertShows<ReaderView>();
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        var sw = Stopwatch.StartNew();
        while (vm.PageLeft is null && sw.ElapsedMilliseconds < 5000) app.Pump();
        return vm;
    }

    [AvaloniaFact]
    public void Settings_screen_shows_and_saves_reader_preferences()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        app.AssertShows<SettingsView>();
        var settings = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);

        settings.TwoPageByDefault  = true;
        settings.OpenInZenMode     = true;
        settings.PageTurnAnimation = false;
        settings.SpeedSlowCommand.Execute(null);

        var saved = app.Root.Repo.Current.Reader;
        Assert.True(saved.TwoPageByDefault);
        Assert.True(saved.OpenInZenMode);
        Assert.False(saved.PageTurnAnimation);
        Assert.Equal("slow", saved.PageTurnSpeed);
        Assert.True(settings.IsSpeedSlow);
        Assert.False(settings.IsSpeedNormal);
    }

    [AvaloniaFact]
    public void Reader_opens_with_the_saved_preferences()
    {
        using var app = ViewSmoke.Start();
        var prefs = app.Root.Repo.Current.Reader;
        prefs.TwoPageByDefault  = true;
        prefs.OpenInZenMode     = true;
        prefs.PageTurnAnimation = false;

        var vm = OpenReader(app);
        Assert.True(vm.IsTwoPageMode);
        Assert.True(vm.IsZenMode);
        Assert.Equal(WindowState.FullScreen, app.Window.WindowState);

        // Sin animación la página cambia sin pintar nada en la capa de la hoja.
        var flipLayer = app.Window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "FlipLayer");
        vm.NextPageCommand.Execute(null);
        Assert.Empty(flipLayer.Children);
        Assert.Equal(2, vm.CurrentPage);
    }

    [AvaloniaFact]
    public void Shortcuts_can_be_rebound_in_settings_and_the_reader_uses_them()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        app.AssertShows<SettingsView>();
        var settings = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);

        // "Página siguiente": primer hueco → pulsar J.
        var next = settings.Shortcuts.Single(r => r.Action.Id == "next");
        next.Slots[0].ClickCommand.Execute(null);
        Assert.True(next.Slots[0].IsCapturing);
        app.Window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.J, RawInputModifiers.None);
        app.Pump();
        Assert.False(next.Slots[0].IsCapturing);
        Assert.Equal("J", next.Slots[0].Text);

        // La flecha izquierda pasa a "siguiente": se le quita a "anterior".
        next.Slots[1].ClickCommand.Execute(null);
        app.Window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        app.Pump();
        var prev = settings.Shortcuts.Single(r => r.Action.Id == "prev");
        Assert.All(prev.Slots, s => Assert.False(s.IsAssigned));
        Assert.Contains("Página anterior", settings.ShortcutNotice);

        // En el lector: J avanza, la flecha derecha ya no.
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.AssertShows<ReaderView>();
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        app.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        app.Pump();
        Assert.Equal(0, vm.CurrentPage);
        app.Window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        app.Pump();
        Assert.Equal(1, vm.CurrentPage);

        // Combinación con modificador: Ctrl+W cierra el lector (de fábrica).
        app.Window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
        app.Pump();
        Assert.IsType<HomeViewModel>(app.Root.Navigation.CurrentViewModel);

        // Restablecer vuelve a las de fábrica.
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        settings = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);
        settings.ResetShortcutsCommand.Execute(null);
        Assert.Equal("→", settings.Shortcuts.Single(r => r.Action.Id == "next").Slots[0].Text);
        Assert.Empty(app.Root.Repo.Current.Reader.Shortcuts);
    }

    [AvaloniaFact]
    public void Page_turn_animation_is_off_for_now_and_hidden_in_settings()
    {
        using var app = ViewSmoke.Start();
        Assert.True(app.Root.Repo.Current.Reader.PageTurnAnimation); // el ajuste guardado da igual

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        Assert.False(vm.AnimatePageTurns);

        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        var settings = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);
        Assert.False(settings.ShowPageTurnSettings);
    }
}
