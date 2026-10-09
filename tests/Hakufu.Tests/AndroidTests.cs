using Avalonia.Headless.XUnit;
using Hakufu.Data;
using Hakufu.I18n;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Lo compartido que cambia en Android (ver docs/superpowers/specs/2026-10-09-android-design.md).</summary>
public class AndroidTests
{
    [Theory]
    [InlineData("content://com.android.externalstorage.documents/tree/primary%3AMangas", "/storage/emulated/0/Mangas")]
    [InlineData("content://com.android.externalstorage.documents/tree/primary%3AMangas%2FShonen/document/primary%3AMangas%2FShonen", "/storage/emulated/0/Mangas/Shonen")]
    [InlineData("content://com.android.externalstorage.documents/tree/primary%3A", "/storage/emulated/0")]
    [InlineData("content://com.android.externalstorage.documents/tree/1A2B-3C4D%3AComics", "/storage/1A2B-3C4D/Comics")]
    [InlineData("content://com.android.providers.downloads.documents/tree/raw%3A%2Fstorage%2Femulated%2F0%2FDownload%2FMangas", "/storage/emulated/0/Download/Mangas")]
    [InlineData("file:///storage/emulated/0/Hakufu", "/storage/emulated/0/Hakufu")]
    public void Tree_uris_become_paths(string uri, string path)
        => Assert.Equal(path, AndroidStorage.TreeUriToPath(new Uri(uri)));

    [Theory]
    [InlineData("content://com.google.android.apps.docs.storage/tree/acc%3D1%3Bdoc%3Dabc")]
    [InlineData("content://com.android.providers.downloads.documents/tree/downloads")]
    [InlineData("https://example.com/tree/primary%3AMangas")]
    public void Folders_not_on_the_device_have_no_path(string uri)
        => Assert.Null(AndroidStorage.TreeUriToPath(new Uri(uri)));

    private static JsonDataRepository NewRepo()
    {
        var repo = new JsonDataRepository();
        repo.LoadAsync().GetAwaiter().GetResult();
        return repo;
    }

    [Fact]
    public void Default_library_root_until_one_is_chosen()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var repo = NewRepo();
        Assert.Equal(Path.Combine(tmp.Root, "Hakufu"), new LibraryScanner(repo).Root);

        repo.Current.LibraryRoot = "/storage/emulated/0/Mangas";
        Assert.Equal("/storage/emulated/0/Mangas", new LibraryScanner(repo).Root);
    }

    [Fact]
    public void Settings_on_android_pick_a_folder_but_do_not_quit()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var root = new CompositionRoot(NewRepo(), new FakeUpdateService());
        root.Navigation.NavigateTo<SettingsViewModel>();
        var vm = Assert.IsType<SettingsViewModel>(root.Navigation.CurrentViewModel);
        Assert.True(vm.CanPickLibraryRoot);
        Assert.False(vm.CanExit);
        // La ruta se enseña como la ve el usuario.
        Assert.Equal("Almacenamiento interno → Hakufu", vm.LibraryRootText);
    }

    [Fact]
    public void Library_on_android_offers_another_folder_and_the_android_help()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var root = new CompositionRoot(NewRepo(), new FakeUpdateService());
        root.Navigation.NavigateTo<LibraryViewModel>();
        var vm = Assert.IsType<LibraryViewModel>(root.Navigation.CurrentViewModel);
        Assert.True(vm.CanPickRoot);
        Assert.True(vm.ShowAndroidEmptyHelp);
        Assert.False(vm.ShowIosEmptyHelp);
        Assert.False(vm.ShowDesktopEmptyHelp);
    }

    [Fact]
    public async Task Saving_on_android_goes_to_downloads_without_a_picker()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var path = await new FilePickerService().SaveFileAsync("Guardar", "Hakufu-2026-10-09.zip", FileFilter.Backup);
        Assert.Equal(Path.Combine(tmp.Root, "Download", "Hakufu-2026-10-09.zip"), path);
        Assert.True(Directory.Exists(Path.Combine(tmp.Root, "Download")));
        Assert.Equal("Almacenamiento interno → Download → Hakufu-2026-10-09.zip", FilePickerService.DisplayPath(path!));
    }

    [Fact]
    public void A_folder_outside_the_device_is_refused()
    {
        Assert.Equal("/storage/emulated/0/Mangas",
            FilePickerService.ToLocalFolder(new Uri("content://com.android.externalstorage.documents/tree/primary%3AMangas")));
        Assert.Throws<FolderNotOnDeviceException>(() =>
            FilePickerService.ToLocalFolder(new Uri("content://com.google.android.apps.docs.storage/tree/acc%3D1%3Bdoc%3Dabc")));
    }

    // Review Focus #2: elegir Drive no cambia la carpeta y se avisa.
    [Fact]
    public async Task Choosing_a_folder_outside_the_device_keeps_the_old_one()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var repo = NewRepo();
        repo.Current.LibraryRoot = Path.Combine(tmp.Root, "Hakufu");
        Directory.CreateDirectory(repo.Current.LibraryRoot);
        var root = new CompositionRoot(repo, new FakeUpdateService());
        var vm = new LibraryViewModel(root.Library, root.Scanner, root.Cover, root.Navigation, new NotOnDevicePicker());
        await vm.RefreshAsync(); // que la lectura del constructor no pise el aviso

        // El selector lanza al momento: el aviso queda puesto al volver de Execute (async void).
        vm.PickRootCommand.Execute(null);

        Assert.Equal(Path.Combine(tmp.Root, "Hakufu"), repo.Current.LibraryRoot);
        Assert.Equal(L.Get("library.folder_not_on_device"), vm.ErrorText);
    }

    // Review Focus #1: sin permiso (o quitado) se explica y se ofrece darlo; al volver, se lee.
    [Fact]
    public async Task Without_permission_the_library_asks_for_it()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root, hasAccess: false);
        var root = new CompositionRoot(NewRepo(), new FakeUpdateService());
        root.Navigation.NavigateTo<LibraryViewModel>();
        var vm = Assert.IsType<LibraryViewModel>(root.Navigation.CurrentViewModel);
        await vm.RefreshAsync();

        Assert.True(vm.NeedsPermission);
        Assert.False(vm.NeedsRoot);
        Assert.False(vm.HasError);
        Assert.False(vm.IsEmpty);

        vm.RequestPermissionCommand.Execute(null);
        Assert.Equal(1, android.Requested);

        android.HasAccess = true;
        Directory.CreateDirectory(Path.Combine(tmp.Root, "Hakufu"));
        await vm.RefreshAsync();
        Assert.False(vm.NeedsPermission);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void Desktop_never_asks_for_permission()
    {
        using var tmp = new TempDataDir();
        var root = new CompositionRoot(NewRepo(), new FakeUpdateService());
        root.Navigation.NavigateTo<LibraryViewModel>();
        Assert.False(Assert.IsType<LibraryViewModel>(root.Navigation.CurrentViewModel).NeedsPermission);
    }

    [AvaloniaFact]
    public void Back_closes_the_dialog_first_then_the_reader_then_goes_up()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        var main = Assert.IsType<MainWindowViewModel>(app.View.DataContext);

        // Inicio: que lo resuelva Android (sale de la app).
        app.AssertShows<HomeView>();
        Assert.False(app.View.HandleBack());

        // Colección → su flecha lleva a Biblioteca.
        app.Root.Navigation.NavigateTo<CollectionDetailViewModel>(app.SampleCollection.Id);
        app.AssertShows<CollectionDetailView>();
        Assert.True(app.View.HandleBack());
        Assert.IsType<LibraryViewModel>(app.Root.Navigation.CurrentViewModel);

        // Review Focus #3: diálogo encima del lector → solo se cierra el diálogo.
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.AssertShows<ReaderView>();
        app.Root.Dialog.ShowModal(new LegalViewModel(app.Root.Navigation));
        Assert.True(main.IsModalOpen);
        Assert.True(app.View.HandleBack());
        Assert.False(main.IsModalOpen);
        Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);

        // Lector → se cierra (guarda el progreso y vuelve a Inicio).
        Assert.True(app.View.HandleBack());
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (app.Root.Navigation.CurrentViewModel is not HomeViewModel && sw.ElapsedMilliseconds < 3000) app.Pump();
        Assert.IsType<HomeViewModel>(app.Root.Navigation.CurrentViewModel);
    }

    private sealed class NotOnDevicePicker : IFilePickerService
    {
        public Task<string[]> PickFilesAsync(string title, FileFilter filter, bool multiSelect = true) => Task.FromResult<string[]>([]);
        public Task<string?> PickImageAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickFolderAsync(string title) => throw new FolderNotOnDeviceException();
        public Task<string?> SaveFileAsync(string title, string suggestedName, FileFilter filter) => Task.FromResult<string?>(null);
        public void OpenFolder(string path) { }
    }
}
