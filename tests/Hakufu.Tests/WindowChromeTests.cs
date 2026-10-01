using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Hakufu.Controls;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Barra de título propia (Linux) y transición de tinta al cambiar de tema.</summary>
public class WindowChromeTests
{
    [AvaloniaFact]
    public void Own_title_bar_is_off_by_default_and_on_when_asked()
    {
        using var app = ViewSmoke.Start();
        Assert.False(app.Window.TitleBar.IsVisible);
        Assert.Equal(SystemDecorations.Full, app.Window.SystemDecorations);

        app.Window.OwnTitleBar = true;
        app.Pump();
        Assert.True(app.Window.TitleBar.IsVisible);
        Assert.True(app.Window.ResizeGrips.IsVisible);
        Assert.Equal(SystemDecorations.None, app.Window.SystemDecorations);
        Assert.True(app.Window.TitleBar.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void Window_buttons_minimize_and_toggle_full_screen()
    {
        using var app = ViewSmoke.Start();
        var w = app.Window;
        w.OwnTitleBar = true;
        app.Pump();

        Click(w.FullScreenButton);
        Assert.Equal(WindowState.FullScreen, w.WindowState);
        Assert.True(w.TitleBar.IsVisible);      // la forma de salir
        Assert.False(w.ResizeGrips.IsVisible);
        Assert.Equal("Salir de pantalla completa", ToolTip.GetTip(w.FullScreenButton));

        Click(w.FullScreenButton);
        Assert.Equal(WindowState.Normal, w.WindowState);
        Assert.True(w.ResizeGrips.IsVisible);
        Assert.Equal("Pantalla completa", ToolTip.GetTip(w.FullScreenButton));

        Click(w.MinimizeButton);
        Assert.Equal(WindowState.Minimized, w.WindowState);
    }

    [AvaloniaFact]
    public void Window_buttons_respond_to_a_real_pointer_click()
    {
        using var app = ViewSmoke.Start();
        var w = app.Window;
        w.OwnTitleBar = true;
        app.Pump();

        // Pulsar y soltar en el centro del botón, como el ratón (hit-test incluido).
        PointerClick(w, w.FullScreenButton);
        Assert.Equal(WindowState.FullScreen, w.WindowState);
        PointerClick(w, w.FullScreenButton);
        Assert.Equal(WindowState.Normal, w.WindowState);

        // Un clic en la barra (fuera de los botones) no hace nada raro.
        var bar = w.TitleText.TranslatePoint(new Point(4, 4), w)!.Value;
        w.MouseDown(bar, MouseButton.Left);
        w.MouseUp(bar, MouseButton.Left);
        app.Pump();
        Assert.Equal(WindowState.Normal, w.WindowState);
        Assert.True(w.IsVisible);

        var closed = false;
        w.Closed += (_, _) => closed = true;
        PointerClick(w, w.CloseButton);
        Assert.True(closed);
    }

    private static void PointerClick(Window w, Control target)
    {
        var p = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), w)!.Value;
        w.MouseMove(p);
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Title_bar_hides_only_in_zen_and_goes_dark_in_the_reader()
    {
        using var app = ViewSmoke.Start();
        var w = app.Window;
        w.OwnTitleBar = true;

        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        var reader = (ReaderViewModel)app.Root.Navigation.CurrentViewModel!;
        app.AssertShows<ReaderView>();
        Assert.Contains("reader", w.TitleBar.Classes);

        reader.IsZenMode = true;
        app.Pump();
        Assert.Equal(WindowState.FullScreen, w.WindowState);
        Assert.False(w.TitleBar.IsVisible);

        reader.IsZenMode = false;
        app.Pump();
        Assert.Equal(WindowState.Normal, w.WindowState);
        Assert.True(w.TitleBar.IsVisible);

        // Pantalla completa elegida con el botón: salir del lector no la quita.
        Click(w.FullScreenButton);
        app.Root.Navigation.NavigateTo<HomeViewModel>();
        app.Pump();
        Assert.Equal(WindowState.FullScreen, w.WindowState);
        Assert.True(w.TitleBar.IsVisible);
        Assert.DoesNotContain("reader", w.TitleBar.Classes);
    }

    [AvaloniaFact]
    public void Theme_transition_hook_runs_only_on_a_real_change()
    {
        var theme = new ThemeService();
        theme.SetTheme(AppTheme.Light);
        var calls = 0;
        AppTheme? target = null;
        theme.Transition = (to, apply) => { calls++; target = to; apply(); };

        theme.SetTheme(AppTheme.Light);
        Assert.Equal(0, calls);
        theme.SetTheme(AppTheme.Dark);
        Assert.Equal(1, calls);
        Assert.Equal(AppTheme.Dark, target);
        Assert.Equal(AppTheme.Dark, theme.CurrentTheme);
        theme.SetTheme(AppTheme.Light); // deja la app como estaba
    }

    [AvaloniaFact]
    public void Ink_shader_compiles_with_the_bundled_skia()
    {
        Assert.True(InkTransition.HasInkShader, $"El shader de tinta no compila (la transición sería un fundido):\n{InkTransition.ShaderErrors}");
    }

    [AvaloniaTheory]
    [InlineData(true)]   // a oscuro: de izquierda a derecha, como el interruptor
    [InlineData(false)]  // de vuelta a claro: de derecha a izquierda
    public void Ink_transition_reveals_the_new_theme_in_the_direction_of_the_switch(bool toDark)
    {
        using var app = ViewSmoke.Start(darkTheme: !toDark);
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        app.AssertShows<SettingsView>();
        app.Root.Theme.Transition = app.Window.PlayThemeTransition;

        app.Root.Theme.SetTheme(toDark ? AppTheme.Dark : AppTheme.Light);
        Assert.True(app.Window.ThemeInk.IsRunning);
        app.Window.ThemeInk.Seek(0.5);
        app.Pump();

        // A mitad: por donde entra la veta ya se ve el tema nuevo; por donde
        // sale sigue el viejo (la foto).
        var (newSide, oldSide) = toDark ? (0.04, 0.96) : (0.96, 0.04);
        bool IsNew(double luma) => toDark ? luma < 60 : luma > 200;
        bool IsOld(double luma) => toDark ? luma > 200 : luma < 60;
        using var frame = app.Window.CaptureRenderedFrame()!;
        var y = (int)(frame.PixelSize.Height * 0.6);
        Assert.True(IsNew(Luma(frame, newSide, y)), "Por donde entra la veta debería verse ya el tema nuevo.");
        Assert.True(IsOld(Luma(frame, oldSide, y)), "Por donde sale debería seguir el tema viejo.");

        app.Window.ThemeInk.Seek(1);
        app.Pump();
        using var done = app.Window.CaptureRenderedFrame()!;
        Assert.True(IsNew(Luma(done, oldSide, y)), "Al final todo es el tema nuevo.");

        app.Root.Theme.Transition = null;
        app.Root.Theme.SetTheme(AppTheme.Light);
    }

    [AvaloniaFact]
    public void Backup_back_button_is_the_same_as_in_settings()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<BackupViewModel>();
        var backup = app.AssertShows<BackupView>();
        var backupButton = backup.GetVisualDescendants().OfType<Button>().First();
        // Se apunta antes de navegar: fuera del árbol el botón ya no vale.
        var (content, classes, size, pos) = Describe(backupButton, app.Window);

        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        var settings = app.AssertShows<SettingsView>();
        var settingsButton = settings.GetVisualDescendants().OfType<Button>().First();

        Assert.Equal("← Inicio", content);
        Assert.Equal(Describe(settingsButton, app.Window), (content, classes, size, pos));
    }

    private static (object?, string, Size, Point?) Describe(Button b, Visual root) =>
        (b.Content, string.Join(' ', b.Classes.Where(c => !c.StartsWith(':'))),
         b.Bounds.Size, b.TranslatePoint(default, root));

    private static void Click(Button b) =>
        b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private static double Luma(WriteableBitmap bmp, double xFrac, int y)
    {
        var x = (int)(bmp.PixelSize.Width * xFrac);
        using var fb = bmp.Lock();
        var at = fb.Address + y * fb.RowBytes + x * 4; // BGRA o RGBA: los grises dan igual
        return 0.3 * Marshal.ReadByte(at) + 0.59 * Marshal.ReadByte(at, 1) + 0.11 * Marshal.ReadByte(at, 2);
    }
}
