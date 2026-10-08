using Avalonia.Controls;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

public class UpdateSettingsTests
{
    [Fact]
    public async Task CheckOnStartup_is_on_by_default_and_survives_a_save()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        Assert.True(repo.Current.Updates.CheckOnStartup);

        repo.Current.Updates.CheckOnStartup = false;
        await repo.SaveAsync();

        var again = new JsonDataRepository();
        await again.LoadAsync();
        Assert.False(again.Current.Updates.CheckOnStartup);
    }
}

public class AppVersionTests
{
    [Theory]
    [InlineData("v0.11.0", "0.10.1", true)]
    [InlineData("0.10.1", "0.10.1", false)]
    [InlineData("v0.10.0", "0.10.1", false)]
    [InlineData("v0.11.0-beta.1", "0.10.1", true)]
    [InlineData("v0.11.0-beta.1", "0.11.0", false)]
    [InlineData("v0.11.0", "0.11.0-beta.2", true)]
    [InlineData("vX", "0.10.1", false)]
    [InlineData("", "0.10.1", false)]
    public void IsNewer(string candidate, string current, bool expected)
        => Assert.Equal(expected, AppVersion.IsNewer(candidate, current));
}

public class UpdateServicePortableTests
{
    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(respond(r));
    }

    private static HttpResponseMessage Json(string tag) =>
        new(System.Net.HttpStatusCode.OK) { Content = new StringContent($"{{\"tag_name\":\"{tag}\",\"prerelease\":false}}") };

    [Fact]
    public async Task Not_installed_newer_tag_is_available_but_not_self_updatable()
    {
        var r = await new UpdateService(new FakeHttp(_ => Json("v99.0.0"))).CheckAsync();
        Assert.Equal(UpdateCheckStatus.Available, r.Status);
        Assert.Equal("99.0.0", r.Version);
        Assert.False(r.CanSelfUpdate);
    }

    [Fact]
    public async Task Same_tag_is_up_to_date()
    {
        var r = await new UpdateService(new FakeHttp(_ => Json("v" + AppVersion.Current))).CheckAsync();
        Assert.Equal(UpdateCheckStatus.UpToDate, r.Status);
    }

    [Fact]
    public async Task Network_error_or_403_is_failed_and_never_throws()
    {
        var down = new FakeHttp(_ => throw new HttpRequestException("sin red"));
        Assert.Equal(UpdateCheckStatus.Failed, (await new UpdateService(down).CheckAsync()).Status);

        var limited = new FakeHttp(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
        Assert.Equal(UpdateCheckStatus.Failed, (await new UpdateService(limited).CheckAsync()).Status);
    }

    [Fact]
    public async Task Sends_a_user_agent_and_asks_for_the_latest_release()
    {
        HttpRequestMessage? seen = null;
        await new UpdateService(new FakeHttp(r => { seen = r; return Json("v0.0.1"); })).CheckAsync();
        Assert.Equal("https://api.github.com/repos/dap0ry/Hakufu/releases/latest", seen!.RequestUri!.ToString());
        Assert.NotEmpty(seen.Headers.UserAgent);
    }
}

public sealed class FakeUpdateService : IUpdateService
{
    public UpdateCheckResult Result = new(UpdateCheckStatus.UpToDate);
    public bool FailDownload, HangDownload, FailApply;
    public int Checks, Downloads;
    public List<string> Log = [];
    public string CurrentVersion => "0.10.1";

    public Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default) { Checks++; return Task.FromResult(Result); }

    public async Task DownloadAsync(Action<int> progress, CancellationToken ct = default)
    {
        Downloads++;
        if (HangDownload) await Task.Delay(Timeout.Infinite, ct);
        progress(50);
        if (FailDownload) throw new HttpRequestException("cortado");
        progress(100);
    }

    public void ApplyAndRestart()
    {
        if (FailApply) throw new InvalidOperationException("sin Update");
        Log.Add("apply");
    }
}

public class UpdateBannerTests
{
    private static (UpdateBannerViewModel vm, FakeUpdateService svc, List<string> urls)
        Make(UpdateCheckResult result, bool checkOnStartup = true)
    {
        var svc = new FakeUpdateService { Result = result };
        var set = new UpdateSettings { CheckOnStartup = checkOnStartup };
        var urls = new List<string>();
        var vm = new UpdateBannerViewModel(svc, set, () => svc.Log.Add("save"), urls.Add, TimeSpan.Zero,
                                           downloadStall: TimeSpan.FromMilliseconds(100));
        return (vm, svc, urls);
    }

    [Fact]
    public async Task Stalled_download_gives_up_and_offers_retry()
    {
        var (vm, svc, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        svc.HangDownload = true;
        await vm.StartAsync();
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(UpdateBannerState.Available, vm.State);
        Assert.Equal("Reintentar", vm.PrimaryText);
    }

    [Fact]
    public async Task Failing_to_apply_does_not_crash_and_offers_retry()
    {
        var (vm, svc, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        svc.FailApply = true;
        await vm.StartAsync();
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation; // no lanza
        Assert.Equal(UpdateBannerState.Available, vm.State);
        Assert.Equal("Reintentar", vm.PrimaryText);
    }

    [Fact]
    public async Task Checking_again_after_downloading_keeps_the_downloaded_update()
    {
        var (vm, svc, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        await vm.StartAsync();
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        var checks = svc.Checks;

        svc.Result = new(UpdateCheckStatus.Available, "0.12.0", true);
        await vm.CheckNowAsync();
        Assert.Equal(checks, svc.Checks);
        Assert.Equal(UpdateBannerState.Ready, vm.State);
        Assert.Contains("0.11.0", vm.CheckStatus);
    }

    [Fact]
    public async Task Up_to_date_or_failed_keeps_the_banner_hidden()
    {
        foreach (var s in new[] { UpdateCheckStatus.UpToDate, UpdateCheckStatus.Failed })
        {
            var (vm, _, _) = Make(new(s));
            await vm.StartAsync();
            Assert.Equal(UpdateBannerState.Hidden, vm.State);
            Assert.False(vm.IsVisible);
        }
    }

    [Fact]
    public async Task Startup_check_is_skipped_when_disabled()
    {
        var (vm, svc, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true), checkOnStartup: false);
        await vm.StartAsync();
        Assert.Equal(0, svc.Checks);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public async Task Available_update_downloads_then_restarts_saving_first()
    {
        var (vm, svc, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        await vm.StartAsync();
        Assert.Equal(UpdateBannerState.Available, vm.State);
        Assert.Contains("0.11.0", vm.Message);
        Assert.Equal("Actualizar", vm.PrimaryText);

        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal(UpdateBannerState.Ready, vm.State);
        Assert.Equal(100, vm.Progress);
        Assert.Equal("Reiniciar", vm.PrimaryText);

        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal(["save", "apply"], svc.Log);
    }

    [Fact]
    public async Task Failed_download_goes_back_to_available_with_retry()
    {
        var (vm, svc, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        svc.FailDownload = true;
        await vm.StartAsync();
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal(UpdateBannerState.Available, vm.State);
        Assert.Equal("Reintentar", vm.PrimaryText);
    }

    [Fact]
    public async Task Cannot_start_a_second_download_while_downloading()
    {
        var (vm, _, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        await vm.StartAsync();
        Assert.True(vm.PrimaryCommand.CanExecute(null));
        vm.SetStateForTest(UpdateBannerState.Downloading);
        Assert.False(vm.PrimaryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Portable_copy_offers_the_download_page()
    {
        var (vm, svc, urls) = Make(new(UpdateCheckStatus.Available, "0.11.0", CanSelfUpdate: false));
        await vm.StartAsync();
        Assert.Equal(UpdateBannerState.Portable, vm.State);
        Assert.Equal("Descargar", vm.PrimaryText);
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal([UpdateBannerViewModel.DownloadPage], urls);
        Assert.Equal(0, svc.Downloads);
    }

    [Fact]
    public async Task Later_hides_and_check_now_reports_in_words()
    {
        var (vm, svc, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        await vm.StartAsync();
        vm.LaterCommand.Execute(null);
        Assert.False(vm.IsVisible);

        svc.Result = new(UpdateCheckStatus.UpToDate);
        await vm.CheckNowAsync();
        Assert.Equal("Ya tienes la última versión.", vm.CheckStatus);

        svc.Result = new(UpdateCheckStatus.Failed);
        await vm.CheckNowAsync();
        Assert.Equal("No se pudo comprobar. ¿Hay conexión a internet?", vm.CheckStatus);

        svc.Result = new(UpdateCheckStatus.Available, "0.12.0", true);
        await vm.CheckNowAsync();
        Assert.Equal("Hay una versión nueva: 0.12.0.", vm.CheckStatus);
        Assert.True(vm.IsVisible);
    }
}

public class UpdateViewTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task Banner_is_hidden_until_there_is_an_update_and_then_shows_it()
    {
        using var app = ViewSmoke.Start();
        var fake = (FakeUpdateService)app.Root.Updates;
        var bar = app.Window.FindControl<Avalonia.Controls.Border>("UpdateBar")!;
        Assert.False(bar.IsVisible);

        fake.Result = new(UpdateCheckStatus.Available, "0.11.0", true);
        await app.Root.UpdateBanner.CheckNowAsync();
        app.Pump();
        Assert.True(bar.IsVisible);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Settings_shows_the_update_section()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        app.Pump();
        var vm = (SettingsViewModel)app.Root.Navigation.CurrentViewModel!;
        Assert.Same(app.Root.UpdateBanner, vm.Updates);
        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
    }
}

public class UpdateRestartTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task Restarting_from_the_reader_logs_the_reading_time_before_saving()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.Pump();
        var log = app.Root.Repo.Current.ReadingLog;
        double Seconds() => log.Sum(d => d.Seconds);
        var before = Seconds();
        await Task.Delay(50);

        double atSave = -1;
        app.Root.PrepareRestart = () => atSave = Seconds();
        ((FakeUpdateService)app.Root.Updates).Result = new(UpdateCheckStatus.Available, "0.11.0", true);
        await app.Root.UpdateBanner.CheckNowAsync();
        app.Root.UpdateBanner.PrimaryCommand.Execute(null);
        await app.Root.UpdateBanner.LastOperation;
        app.Root.UpdateBanner.PrimaryCommand.Execute(null);
        await app.Root.UpdateBanner.LastOperation;

        Assert.True(atSave > before, $"antes {before}, al guardar {atSave}");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void About_shows_the_full_version_including_beta()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        var vm = (SettingsViewModel)app.Root.Navigation.CurrentViewModel!;
        Assert.Equal($"Versión {AppVersion.Current}", vm.VersionText);
    }
}
