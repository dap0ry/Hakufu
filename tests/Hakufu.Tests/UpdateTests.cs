using Hakufu.Data;
using Hakufu.MVVM.Model;
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
