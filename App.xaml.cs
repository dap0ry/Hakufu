using System.Windows;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;
using Velopack;

namespace Hakufu;

public partial class App : Application
{
    private IDataRepository?  _repo;
    private DateTime          _sessionStart;
    private ITrayIconService? _trayIcon;
    private MainWindow?       _mainWindowRef;

    public App()
    {
        // Debe ejecutarse antes que cualquier otra cosa: gestiona los
        // hooks de instalar/actualizar/desinstalar de Velopack (crea el
        // acceso directo del menú Inicio en la primera instalación,
        // limpia versiones antiguas, etc.).
        VelopackApp.Build().Run();
    }

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
            var themeService   = new ThemeService();
            var dialogService  = new DialogService();
            var filePickerSvc  = new FilePickerService();
            var coverService   = new CoverService();
            var libraryService = new LibraryService(_repo);
            var profileService = new ProfileService(_repo);
            var updateService  = new UpdateService();
            var sessionService = new SessionService();
            var apiClient      = new HakufuApiClient(sessionService);
            var driveService   = new GoogleDriveService(sessionService);

            // Si ya hay una actualización descargada de la sesión anterior
            // (Velopack lo recuerda entre reinicios), aplícala y reinicia
            // antes de mostrar nada — el usuario nunca ve una pantalla de
            // actualizaciones, simplemente la app ya está al día.
            if (updateService.IsUpdateReadyToApply)
            {
                updateService.ApplyUpdateAndRestart();
                return;
            }

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
                        libraryService, coverService, navService!, sessionService, apiClient),

                    nameof(AccountViewModel) => new AccountViewModel(sessionService, apiClient, navService!),

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

                    nameof(SyncViewModel) => new SyncViewModel(
                        sessionService, apiClient, navService!, libraryService, coverService, _repo!),

                    nameof(BackupViewModel) => new BackupViewModel(
                        driveService, apiClient, navService!, _repo!, coverService),

                    nameof(FriendsViewModel) => new FriendsViewModel(sessionService, apiClient, navService!),

                    nameof(PublicProfileViewModel) when param is string username =>
                        new PublicProfileViewModel(username, apiClient, navService!),

                    _ => throw new InvalidOperationException($"Unknown ViewModel: {type.Name}")
                };
            }

            navService = new NavigationService(Factory);

            // ── Main window ─────────────────────────────────────────
            var mainVm = new MainWindowViewModel(navService, dialogService);
            var window = new MainWindow { DataContext = mainVm };
            MainWindow     = window;
            _mainWindowRef = window;
            window.Show();

            // Comprobar/descargar actualizaciones en segundo plano, sin
            // bloquear el arranque ni avisar al usuario.
            _ = updateService.CheckForUpdatesInBackgroundAsync();

            // ── Bandeja del sistema ──────────────────────────────────
            // La X de la ventana la oculta (ver MainWindow.Window_Closing);
            // esto es lo que permite recuperarla o cerrar la app de verdad.
            _trayIcon = new TrayIconService();
            _trayIcon.Initialize(
                onRestore: () => _mainWindowRef?.RestoreFromTray(),
                onExit:    () => _mainWindowRef?.RequestExit());
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
        // Sin esto el icono se queda "fantasma" en la bandeja tras salir —
        // Environment.Exit no ejecuta Dispose de forma fiable.
        _trayIcon?.Dispose();
        base.OnExit(e);
        // pdfium (Docnet.Core) keeps native threads alive; force-kill the process
        // after all cleanup so the app doesn't linger in Task Manager.
        Environment.Exit(e.ApplicationExitCode);
    }
}
