using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

/// <summary>Copia de seguridad: elegir si van colecciones y cuáles.</summary>
public class BackupOptionsTests
{
    [AvaloniaFact]
    public void Export_options_list_collections_and_summarize_the_choice()
    {
        foreach (var dark in new[] { false, true })
        {
            using var app = ViewSmoke.Start(darkTheme: dark);
            app.Root.Navigation.NavigateTo<BackupViewModel>();
            app.AssertShows<BackupView>();
            var vm = Assert.IsType<BackupViewModel>(app.Root.Navigation.CurrentViewModel);

            var option = Assert.Single(vm.CollectionOptions);
            Assert.True(option.IsSelected);                       // todas marcadas al entrar
            Assert.Equal("Tu perfil y 1 de 1 colección", vm.ExportSummary);

            vm.SelectNoCollectionsCommand.Execute(null);
            Assert.Equal("Solo tu perfil", vm.ExportSummary);
            vm.SelectAllCollectionsCommand.Execute(null);
            vm.IncludeCollections = false;
            Assert.Equal("Solo tu perfil", vm.ExportSummary);

            vm.IncludeCollections = true;
            if (Environment.GetEnvironmentVariable("BACKUP_SHOT") is { } shot)
            {
                for (var i = 0; i < 30; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
                app.Window.CaptureRenderedFrame()!.Save(Path.Combine(shot, $"backup-{(dark ? "dark" : "light")}.png"));
            }
        }
    }
}
