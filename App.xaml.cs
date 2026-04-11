using System.Windows;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu;

public partial class App : Application
{
    private IDataRepository? _repo;
    private DateTime _sessionStart;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _sessionStart = DateTime.Now;
        base.OnStartup(e);

        try
        {
            // ── Data layer ──────────────────────────────────────────
            _repo = new JsonDataRepository();
            await _repo.LoadAsync();

            // ── Services ────────────────────────────────────────────
            var themeService  = new ThemeService();
            var dialogService = new DialogService();
            var filePickerSvc = new FilePickerService();
            var coverService  = new CoverService();
            var libraryService = new LibraryService(_repo);
            var profileService = new ProfileService(_repo);
            var updateService  = new UpdateService();
            var storeService   = new StoreService();

            // Apply saved theme
            var savedTheme = _repo.Current.ActiveTheme == "Dark" ? AppTheme.Dark : AppTheme.Light;
            themeService.SetTheme(savedTheme);

            // ── Navigation factory ──────────────────────────────────
            NavigationService? navService = null;

            BaseViewModel Factory(Type type, object? param)
            {
                return type.Name switch
                {
                    nameof(HomeViewModel) => new HomeViewModel(
                        libraryService, coverService, navService!, updateService, storeService),

                    nameof(LibraryViewModel) => new LibraryViewModel(
                        libraryService, coverService, dialogService, navService!),

                    nameof(CollectionDetailViewModel) when param is Guid id => new CollectionDetailViewModel(
                        id, libraryService, coverService, dialogService, navService!, filePickerSvc),

                    nameof(ReaderViewModel) when param is ReaderNavigationParam p => new ReaderViewModel(
                        p.Manga, p.StartPage, libraryService, profileService, navService!),

                    nameof(ProfileViewModel) => new ProfileViewModel(
                        profileService, libraryService, coverService, dialogService, navService!),

                    nameof(SettingsViewModel) => new SettingsViewModel(themeService, _repo, navService!, dialogService, libraryService),

                    nameof(HelpViewModel) => new HelpViewModel(navService!),

                    nameof(UpdateViewModel) => new UpdateViewModel(updateService, navService!),

                    nameof(StoreViewModel)  => new StoreViewModel(storeService, navService!),

                    _ => throw new InvalidOperationException($"Unknown ViewModel: {type.Name}")
                };
            }

            navService = new NavigationService(Factory);

            // ── Main window ─────────────────────────────────────────
            var mainVm = new MainWindowViewModel(navService, dialogService);
            var window = new MainWindow { DataContext = mainVm };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al iniciar Hakufu:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_repo is not null)
        {
            var elapsed = (long)(DateTime.Now - _sessionStart).TotalSeconds;
            _repo.Current.TotalUsageSeconds += elapsed;
            _repo.SaveAsync().GetAwaiter().GetResult();
        }
        base.OnExit(e);
        // pdfium (Docnet.Core) keeps native threads alive; force-kill the process
        // after all cleanup so the app doesn't linger in Task Manager.
        Environment.Exit(e.ApplicationExitCode);
    }
}
