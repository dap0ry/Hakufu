using Avalonia.Headless.XUnit;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests.Views;

public class Task5ViewTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Backup_view_renders(bool dark)
    {
        using var app = ViewSmoke.Start(darkTheme: dark);
        app.Root.Navigation.NavigateTo<BackupViewModel>();
        app.AssertShows<BackupView>();
    }
}
