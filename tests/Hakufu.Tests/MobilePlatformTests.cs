using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>
/// Lo que pone la cabecera de iOS antes de arrancar (AppPlatform y AppPaths),
/// simulado aquí para probar lo compartido. Al acabar deja todo como en escritorio.
/// </summary>
public sealed class MobileMode : IDisposable
{
    public MobileMode(string libraryRoot)
    {
        AppPlatform.IsMobile = true;
        AppPaths.FixedLibraryRoot = libraryRoot;
    }

    public void Dispose()
    {
        AppPlatform.IsMobile = false;
        AppPaths.FixedLibraryRoot = null;
    }
}

/// <summary>
/// Lo que pone Platforms/Android antes de arrancar: móvil, sin biblioteca fija, carpeta por
/// defecto y Descargas dentro de <paramref name="storageRoot"/>. Al acabar, como en escritorio.
/// </summary>
public sealed class AndroidMode : IDisposable
{
    public AndroidMode(string storageRoot, bool hasAccess = true)
    {
        HasAccess = hasAccess;
        AppPlatform.IsMobile = true;
        AppPlatform.HasLibraryAccess = () => HasAccess;
        AppPlatform.RequestLibraryAccess = () => Requested++;
        AppPaths.DefaultLibraryRoot = Path.Combine(storageRoot, "Hakufu");
        AppPaths.SaveDirOverride = Path.Combine(storageRoot, "Download");
        AppPaths.VisibleRoot = (storageRoot, "files.android_storage");
    }

    public bool HasAccess { get; set; }
    public int Requested { get; private set; }

    public void Dispose()
    {
        AppPlatform.IsMobile = false;
        AppPlatform.HasLibraryAccess = null;
        AppPlatform.RequestLibraryAccess = null;
        AppPaths.DefaultLibraryRoot = null;
        AppPaths.SaveDirOverride = null;
        AppPaths.VisibleRoot = null;
    }
}

public class MobilePlatformTests
{
    private static JsonDataRepository NewRepo()
    {
        var repo = new JsonDataRepository();
        repo.LoadAsync().GetAwaiter().GetResult();
        return repo;
    }

    [Fact]
    public void Fixed_library_root_wins_over_the_saved_one()
    {
        using var tmp = new TempDataDir();
        var documents = Path.Combine(tmp.Root, "Documents");
        Directory.CreateDirectory(documents);
        var repo = NewRepo();
        repo.Current.LibraryRoot = "/otra/carpeta";

        using (new MobileMode(documents))
            Assert.Equal(documents, new LibraryScanner(repo).Root);

        Assert.Equal("/otra/carpeta", new LibraryScanner(repo).Root);
    }

    // Review Focus #3: en iOS una instalación nueva no pide elegir carpeta.
    [Fact]
    public async Task Empty_fixed_library_is_empty_not_unchosen()
    {
        using var tmp = new TempDataDir();
        var documents = Path.Combine(tmp.Root, "Documents");
        Directory.CreateDirectory(documents);
        var repo = NewRepo();

        using var mobile = new MobileMode(documents);
        var root = new CompositionRoot(repo, new FakeUpdateService());
        await root.Scanner.ScanAsync();
        root.Navigation.NavigateTo<LibraryViewModel>();
        var vm = Assert.IsType<LibraryViewModel>(root.Navigation.CurrentViewModel);
        await vm.RefreshAsync();

        Assert.False(vm.NeedsRoot);
        Assert.True(vm.IsEmpty);
        Assert.True(vm.IsMobile);
    }

    [Fact]
    public void Settings_hide_folder_picker_and_quit_on_mobile()
    {
        using var tmp = new TempDataDir();
        var repo = NewRepo();
        var root = new CompositionRoot(repo, new FakeUpdateService());

        root.Navigation.NavigateTo<SettingsViewModel>();
        var desktop = Assert.IsType<SettingsViewModel>(root.Navigation.CurrentViewModel);
        Assert.True(desktop.CanPickLibraryRoot);
        Assert.True(desktop.CanExit);

        using var mobile = new MobileMode(tmp.Root);
        root.Navigation.NavigateTo<SettingsViewModel>();
        var phone = Assert.IsType<SettingsViewModel>(root.Navigation.CurrentViewModel);
        Assert.False(phone.CanPickLibraryRoot);
        Assert.False(phone.CanExit);
    }

    // En iOS la carpeta de datos no se ve en Archivos: no se enseña ni se ofrece abrirla.
    [Fact]
    public void Backup_hides_the_data_folder_on_mobile()
    {
        using var tmp = new TempDataDir();
        var root = new CompositionRoot(NewRepo(), new FakeUpdateService());

        root.Navigation.NavigateTo<BackupViewModel>();
        Assert.True(Assert.IsType<BackupViewModel>(root.Navigation.CurrentViewModel).ShowDataFolder);

        using var mobile = new MobileMode(tmp.Root);
        root.Navigation.NavigateTo<BackupViewModel>();
        Assert.False(Assert.IsType<BackupViewModel>(root.Navigation.CurrentViewModel).ShowDataFolder);
    }

    [Fact]
    public void Open_folder_and_url_use_the_platform_hooks()
    {
        using var tmp = new TempDataDir();
        string? folder = null, url = null;
        AppPlatform.OpenFolder = p => folder = p;
        AppPlatform.OpenUrl    = u => url = u;
        try
        {
            new FilePickerService().OpenFolder(tmp.Root);
            UrlOpener.Open("https://github.com/dap0ry/Hakufu/releases");

            Assert.Equal(tmp.Root, folder);
            Assert.Equal("https://github.com/dap0ry/Hakufu/releases", url);
        }
        finally
        {
            AppPlatform.OpenFolder = null;
            AppPlatform.OpenUrl    = null;
        }
    }

    private const string OldHome = "/private/var/mobile/Containers/Data/Application/11111111-2222-3333-4444-555555555555";
    private const string NewHome = "/private/var/mobile/Containers/Data/Application/AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE";

    // Review Focus #1: iOS cambia la ruta del contenedor al reinstalar.
    [Fact]
    public void Rebase_moves_saved_paths_to_the_current_container()
    {
        var store = new AppDataStore { LibraryRoot = $"{OldHome}/Documents" };
        store.Mangas.Add(new Manga
        {
            FilePath       = $"{OldHome}/Documents/Berserk/Tomo 1.cbz",
            CoverCachePath = $"{OldHome}/Library/Application Support/Hakufu/covers/x.png",
        });
        store.Profile.AvatarPath = $"{OldHome}/Library/Application Support/Hakufu/profile/yo.png";

        Assert.True(ContainerPaths.Rebase(store, NewHome));

        Assert.Equal($"{NewHome}/Documents", store.LibraryRoot);
        Assert.Equal($"{NewHome}/Documents/Berserk/Tomo 1.cbz", store.Mangas[0].FilePath);
        Assert.Equal($"{NewHome}/Library/Application Support/Hakufu/covers/x.png", store.Mangas[0].CoverCachePath);
        Assert.Equal($"{NewHome}/Library/Application Support/Hakufu/profile/yo.png", store.Profile.AvatarPath);
        Assert.False(ContainerPaths.Rebase(store, NewHome)); // ya está al día
    }

    [Fact]
    public void Rebase_leaves_paths_outside_a_container_alone()
    {
        var store = new AppDataStore { LibraryRoot = "/home/dani/Mangas" };
        store.Mangas.Add(new Manga { FilePath = "/home/dani/Mangas/a.cbz", CoverCachePath = "" });

        Assert.False(ContainerPaths.Rebase(store, NewHome));
        Assert.Equal("/home/dani/Mangas", store.LibraryRoot);
        Assert.Equal("/home/dani/Mangas/a.cbz", store.Mangas[0].FilePath);
        Assert.Equal("", store.Mangas[0].CoverCachePath);
    }

    [Fact]
    public void Data_dir_override_is_used_unless_the_env_var_is_set()
    {
        using var tmp = new TempDataDir(); // pone HAKUFU_DATA_DIR
        AppPaths.DataDirOverride = "/ios/Library/Application Support/Hakufu";
        try
        {
            Assert.Equal(tmp.DataDir, AppPaths.DataDir); // la variable manda (tests)
            Environment.SetEnvironmentVariable("HAKUFU_DATA_DIR", null);
            Assert.Equal("/ios/Library/Application Support/Hakufu", AppPaths.DataDir);
        }
        finally
        {
            AppPaths.DataDirOverride = null;
        }
    }
}
