using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
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

    // Al volver de dar el permiso, la carpeta por defecto aún no existe: leerla la crea (vacía)
    // en vez de decir que no se puede leer. Una carpeta elegida que falta sí es un error.
    [Fact]
    public async Task Scanning_creates_the_missing_default_folder_but_not_a_chosen_one()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var repo = NewRepo();

        var result = await new LibraryScanner(repo).ScanAsync();
        Assert.Equal(ScanStatus.Ok, result.Status);
        Assert.True(Directory.Exists(Path.Combine(tmp.Root, "Hakufu")));

        var chosen = Path.Combine(tmp.Root, "SD", "Mangas");
        repo.Current.LibraryRoot = chosen;
        Assert.Equal(ScanStatus.Unreadable, (await new LibraryScanner(repo).ScanAsync()).Status);
        Assert.False(Directory.Exists(chosen));
    }

    // En Android la vista ya está puesta cuando App engancha el botón de atrás (y las zonas
    // seguras): esperar a AttachedToVisualTree no servía y atrás cerraba la app.
    [AvaloniaFact]
    public void Back_is_hooked_even_when_the_view_is_already_shown()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        app.Root.Navigation.NavigateTo<LibraryViewModel>();
        app.AssertShows<LibraryView>();

        App.HookBack(app.View);
        var back = new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.TopLevel.BackRequestedEvent);
        app.Host.RaiseEvent(back);

        Assert.True(back.Handled);
        Assert.IsType<HomeViewModel>(app.Root.Navigation.CurrentViewModel);
    }

    // Android: el botón de atrás llega antes como tecla Escape (Avalonia: Keycode.Back → Key.Escape).
    // El lector se la quedaba siempre (atajo «salir del modo zen») y atrás no salía del lector.
    // Fuera del modo zen tiene que pasar; dentro, sale del modo zen.
    [AvaloniaFact]
    public void Back_key_reaches_android_unless_it_exits_zen()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        app.AssertShows<ReaderView>();
        var reader = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);

        Assert.False(PressEscape(app.Host).Handled);

        reader.IsZenMode = true;
        Assert.True(PressEscape(app.Host).Handled);
        Assert.False(reader.IsZenMode);
    }

    private static KeyEventArgs PressEscape(TopLevel top)
    {
        var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        top.RaiseEvent(e);
        return e;
    }

    // Review Focus #1: sin permiso, Android deja ver las carpetas pero no sus archivos. Leer así
    // quitaría de data.json todos los tomos (y su progreso): sin permiso no se lee nada.
    [Fact]
    public async Task Without_permission_the_scan_touches_nothing()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var repo = NewRepo();
        var serie = Path.Combine(tmp.Root, "Hakufu", "Serie");
        var tomo = Fixtures.MakeCbz(serie, "Tomo 1.cbz", "1.png");
        var scanner = new LibraryScanner(repo);
        Assert.Equal(ScanStatus.Ok, (await scanner.ScanAsync()).Status);
        Assert.Single(repo.Current.Mangas);

        android.HasAccess = false;
        File.Delete(tomo); // lo que «ve» Android sin permiso: la carpeta, sin sus archivos
        Assert.Equal(ScanStatus.Unreadable, (await scanner.ScanAsync()).Status);
        Assert.Single(repo.Current.Mangas);
    }

    [Fact]
    public async Task Without_permission_the_collections_hide_behind_the_notice()
    {
        using var tmp = new TempDataDir();
        using var android = new AndroidMode(tmp.Root);
        var repo = NewRepo();
        Fixtures.MakeCbz(Path.Combine(tmp.Root, "Hakufu", "Serie"), "Tomo 1.cbz", "1.png");
        var root = new CompositionRoot(repo, new FakeUpdateService());
        await root.Scanner.ScanAsync();

        android.HasAccess = false;
        root.Navigation.NavigateTo<LibraryViewModel>();
        var vm = Assert.IsType<LibraryViewModel>(root.Navigation.CurrentViewModel);
        await vm.RefreshAsync();

        Assert.True(vm.NeedsPermission);
        Assert.False(vm.ShowCollections);
        Assert.False(vm.HasError);
    }

    // Los iconos de la barra de estado tienen que verse: claros sobre el lector (siempre oscuro)
    // y con el tema oscuro; oscuros con el claro. Antes, en el lector salían negros sobre negro.
    [AvaloniaFact]
    public void System_bar_icons_follow_what_is_underneath()
    {
        bool? overDark = null;
        AppPlatform.SetSystemBarsOverDark = dark => overDark = dark;
        try
        {
            using var app = ViewSmoke.StartMobile(390, 844);
            app.AssertShows<HomeView>();
            app.View.RefreshSystemBars();
            Assert.False(overDark);

            app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
            app.AssertShows<ReaderView>();
            Assert.True(overDark);

            app.Root.Navigation.NavigateTo<HomeViewModel>();
            app.AssertShows<HomeView>();
            Assert.False(overDark);

            app.Root.Theme.SetTheme(AppTheme.Dark);
            app.Pump();
            Assert.True(overDark);
            app.Root.Theme.SetTheme(AppTheme.Light);
        }
        finally
        {
            AppPlatform.SetSystemBarsOverDark = null;
        }
    }

    // Móvil en horizontal (unos 914×411): la columna del menú de Inicio no cabía y «Ajustes»
    // pisaba los créditos. Con poca altura (clase "short" de MainView) el menú se encoge.
    [AvaloniaFact]
    public void Home_menu_fits_a_landscape_phone()
    {
        using var app = ViewSmoke.StartMobile(914, 411);
        var home = app.AssertShows<HomeView>();
        var grid = home.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "MenuGrid");
        var items = grid.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("menuItem")).ToList();
        var credits = grid.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == L.Get("home.credit_dev"));
        double Top(Visual v) => v.TranslatePoint(new Point(0, 0), grid)!.Value.Y;
        double Bottom(Visual v) => v.TranslatePoint(new Point(0, v.Bounds.Height), grid)!.Value.Y;

        Assert.True(Bottom(items[^1]) <= Top(credits),
            $"El menú acaba en {Bottom(items[^1]):0} y los créditos empiezan en {Top(credits):0}");
        Assert.True(Top(items[0]) >= 0, $"El menú empieza en {Top(items[0]):0}, por encima de la columna");
    }

    // Android: PdfRenderer da RGBA y la GPU del emulador (y de algunos móviles) no admite texturas
    // BGRA: la página del PDF salía en blanco. Se le pasa a Skia tal cual, en RGBA.
    [AvaloniaFact]
    public void Rgba_pixels_become_an_rgba_bitmap_with_the_same_colors()
    {
        byte[] red = [220, 10, 20, 255];
        using var bmp = BitmapHelper.FromRgba(red, 1, 1);
        Assert.Equal(Avalonia.Platform.PixelFormat.Rgba8888, ((Avalonia.Media.Imaging.WriteableBitmap)bmp).Format);

        var back = new byte[4];
        var pin = System.Runtime.InteropServices.GCHandle.Alloc(back, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { bmp.CopyPixels(new Avalonia.PixelRect(0, 0, 1, 1), pin.AddrOfPinnedObject(), 4, 4); }
        finally { pin.Free(); }
        Assert.Equal(red, back);
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
