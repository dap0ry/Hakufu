using Avalonia.Headless;
using Avalonia.Threading;
using Hakufu.MVVM.View.Controls;
using Hakufu.MVVM.ViewModel;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Hakufu.Tests.Views;

/// <summary>Todas las pantallas interiores usan la misma cabecera (la de Biblioteca).</summary>
public class PageHeaderTests
{
    [AvaloniaFact]
    public void Inner_screens_share_the_big_title_header()
    {
        using var app = ViewSmoke.Start();
        var screens = new (Action go, string title)[]
        {
            (() => app.Root.Navigation.NavigateTo<SettingsViewModel>(), "Ajustes"),
            (() => app.Root.Navigation.NavigateTo<BackupViewModel>(),   "Copia de seguridad"),
            (() => app.Root.Navigation.NavigateTo<HelpViewModel>(),     "Ayuda"),
            (() => app.Root.Navigation.NavigateTo<LegalViewModel>(),    "Aviso legal"),
            (() => app.Root.Navigation.NavigateTo<ProfileViewModel>(),  "Perfil"),
        };
        var shot = Environment.GetEnvironmentVariable("HEADER_SHOT");
        foreach (var (go, title) in screens)
        {
            go();
            for (var i = 0; i < 20; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); if (shot != null) Thread.Sleep(10); }
            var header = Assert.Single(app.Window.GetVisualDescendants().OfType<PageHeader>());
            Assert.Equal(title, header.Title);
            Assert.True(header.Bounds.Height > 60);
            if (shot != null) app.Window.CaptureRenderedFrame()!.Save(Path.Combine(shot, $"{title}.png"));
        }
    }
}
