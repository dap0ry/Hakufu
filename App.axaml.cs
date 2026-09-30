using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu;

public partial class App : Application
{
    private IDataRepository? _repo;
    private DateTime         _sessionStart;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _sessionStart = DateTime.Now;

            // ── Data layer ──────────────────────────────────────────
            // Se carga de forma síncrona antes de crear la ventana: data.json
            // es pequeño y así la primera pantalla ya sale con la biblioteca.
            _repo = new JsonDataRepository();
            _repo.LoadAsync().GetAwaiter().GetResult();

            var mainWindow = new MainWindow();
            try
            {
                mainWindow.DataContext = Compose(_repo);
            }
            catch (Exception ex)
            {
                mainWindow.Content = new TextBlock
                {
                    Text = $"Error al iniciar Hakufu:\n{ex.Message}",
                    Margin = new Thickness(24)
                };
            }
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, e) =>
            {
                SaveOnExit();
                // pdfium (Docnet.Core) deja hilos nativos vivos; forzar la salida
                // para que el proceso no se quede colgado tras cerrar.
                Environment.Exit(e.ApplicationExitCode);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainWindowViewModel Compose(IDataRepository repo)
    {
        // ── Services ────────────────────────────────────────────────
        var themeService         = new ThemeService();
        var dialogService        = new DialogService();
        var filePickerSvc        = new FilePickerService();
        var coverService         = new CoverService();
        var libraryService       = new LibraryService(repo);
        var profileService       = new ProfileService(repo);
        var customizationService = new CustomizationService();
        var wallpaperService     = new WallpaperService();
        var backupService        = new BackupService(repo);

        // Apply saved theme
        themeService.SetTheme(repo.Current.ActiveTheme == "Dark" ? AppTheme.Dark : AppTheme.Light);

        // Wallpaper general (si hay uno guardado) — sustituye el recurso
        // AppBackground antes de crear ninguna vista.
        var wallpaper = repo.Current.Customization.GeneralWallpaper;
        wallpaperService.Apply(wallpaper?.Path, wallpaper?.Opacity ?? 0.3);

        // ── Navigation factory ──────────────────────────────────────
        NavigationService? navService = null;

        BaseViewModel Factory(Type type, object? param) => type.Name switch
        {
            nameof(HomeViewModel) => new HomeViewModel(
                libraryService, coverService, navService!, repo),

            nameof(LibraryViewModel) => new LibraryViewModel(
                libraryService, coverService, dialogService, navService!),

            nameof(CollectionDetailViewModel) when param is Guid id => new CollectionDetailViewModel(
                id, libraryService, coverService, dialogService, navService!, filePickerSvc),

            nameof(ReaderViewModel) when param is ReaderNavigationParam p => new ReaderViewModel(
                p.Manga, p.StartPage, libraryService, profileService, navService!),

            nameof(ProfileViewModel) => new ProfileViewModel(
                profileService, libraryService, coverService, dialogService, navService!),

            nameof(SettingsViewModel) => new SettingsViewModel(
                themeService, repo, navService!, dialogService, libraryService, filePickerSvc),

            nameof(CustomizationViewModel) => new CustomizationViewModel(
                repo, navService!, customizationService, filePickerSvc, wallpaperService),

            nameof(HelpViewModel) => new HelpViewModel(navService!),

            nameof(BackupViewModel) => new BackupViewModel(
                backupService, filePickerSvc, navService!, repo, themeService, wallpaperService),

            _ => throw new InvalidOperationException($"Unknown ViewModel: {type.Name}")
        };

        navService = new NavigationService(Factory);
        return new MainWindowViewModel(navService, dialogService);
    }

    private void SaveOnExit()
    {
        if (_repo is null) return;
        var elapsed = (long)(DateTime.Now - _sessionStart).TotalSeconds;
        _repo.Current.TotalUsageSeconds += elapsed;
        _repo.SaveAsync().GetAwaiter().GetResult();
    }
}
