using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
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

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Storage_manager_modal_renders_collection_tree(bool dark)
    {
        using var app = ViewSmoke.Start(darkTheme: dark);
        var view = app.AssertModal<StorageManagerView>(
            new StorageManagerViewModel(app.Root.Dialog, app.Root.Library, app.Root.FilePicker));

        // La colección de ejemplo sale expandida con sus dos tomos: 1 + 2 casillas.
        var checkBoxes = view.GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(3, checkBoxes.Count);
    }

    [AvaloniaFact]
    public void Selecting_a_whole_collection_locks_its_volumes()
    {
        using var app = ViewSmoke.Start();
        var vm = new StorageManagerViewModel(app.Root.Dialog, app.Root.Library, app.Root.FilePicker);
        var view = app.AssertModal<StorageManagerView>(vm);

        vm.Collections[0].IsSelected = true;
        app.Pump();

        var volumeBoxes = view.GetVisualDescendants().OfType<CheckBox>().Skip(1).ToList();
        Assert.All(volumeBoxes, cb => Assert.False(cb.IsEffectivelyEnabled));
        Assert.True(vm.HasSelection);
    }
}
