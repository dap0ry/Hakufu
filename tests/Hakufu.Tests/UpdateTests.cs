using Hakufu.Data;
using Hakufu.MVVM.Model;

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
