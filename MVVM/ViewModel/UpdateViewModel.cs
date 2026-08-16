using System.Linq;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class UpdateViewModel : BaseViewModel
{
    private readonly IUpdateService     _svc;
    private readonly INavigationService _nav;
    private string? _downloadUrl;

    private string _currentVersion    = "";
    private string _latestVersion     = "—";
    private string _changelog         = "";
    private bool   _isChecking        = true;
    private bool   _isUpdateAvailable = false;
    private bool   _isUpToDate        = false;
    private bool   _isRestartReady    = false;
    private bool   _hasError          = false;
    private string _statusMessage     = "Comprobando actualizaciones…";

    public string CurrentVersion
    {
        get => _currentVersion;
        private set => SetProperty(ref _currentVersion, value);
    }
    public string LatestVersion
    {
        get => _latestVersion;
        private set => SetProperty(ref _latestVersion, value);
    }
    public string Changelog
    {
        get => _changelog;
        private set => SetProperty(ref _changelog, value);
    }
    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetProperty(ref _isUpdateAvailable, value);
    }
    public bool IsUpToDate
    {
        get => _isUpToDate;
        private set => SetProperty(ref _isUpToDate, value);
    }
    public bool IsRestartReady
    {
        get => _isRestartReady;
        private set => SetProperty(ref _isRestartReady, value);
    }
    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public UpdateViewModel(IUpdateService svc, INavigationService nav)
    {
        _svc = svc;
        _nav = nav;
        var v = svc.GetCurrentVersion();
        CurrentVersion  = $"v{v.Major}.{v.Minor}.{v.Build}";
        IsRestartReady  = svc.IsUpdateReadyToApply;
        _ = CheckAsync();
    }

    private async Task CheckAsync()
    {
        IsChecking        = true;
        HasError          = false;
        IsUpdateAvailable = false;
        IsUpToDate        = false;
        StatusMessage     = "Comprobando actualizaciones…";
        // El fondo pudo terminar de descargar mientras tanto — refresca.
        IsRestartReady    = _svc.IsUpdateReadyToApply;

        try
        {
            var release = await _svc.FetchLatestReleaseAsync();
            if (release is null)
            {
                StatusMessage = "No se pudo obtener información de la versión.";
                HasError      = true;
                return;
            }

            var tag = release.TagName.TrimStart('v');
            if (!Version.TryParse(tag, out var latest))
            {
                StatusMessage = "Formato de versión desconocido.";
                HasError      = true;
                return;
            }

            var current = _svc.GetCurrentVersion();
            LatestVersion = $"v{latest.Major}.{latest.Minor}.{latest.Build}";
            Changelog     = release.Body;
            _downloadUrl  = release.Assets.FirstOrDefault()?.BrowserDownloadUrl;

            IsUpdateAvailable = latest > current;
            IsUpToDate        = !IsUpdateAvailable;
            StatusMessage     = IsUpdateAvailable
                ? "¡Nueva versión disponible!"
                : "Hakufu está actualizado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al comprobar: {ex.Message}";
            HasError      = true;
        }
        finally
        {
            IsChecking = false;
        }
    }

    // Aplica la actualización que Velopack ya descargó en segundo plano
    // y reinicia la app.
    public RelayCommand RestartCommand => new(() => _svc.ApplyUpdateAndRestart());

    public RelayCommand ViewOnGitHubCommand => new(
        () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            _downloadUrl ?? "https://github.com/dap0ry/Hakufu/releases/latest")
            { UseShellExecute = true }));

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    public RelayCommand CheckAgainCommand => new(
        () => _ = CheckAsync(),
        () => !IsChecking);
}
