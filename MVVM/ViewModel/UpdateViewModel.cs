using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class UpdateViewModel : BaseViewModel
{
    private readonly IUpdateService _svc;
    private string? _downloadUrl;

    private string _currentVersion    = "";
    private string _latestVersion     = "—";
    private string _changelog         = "";
    private double _downloadProgress  = 0;
    private bool   _isChecking        = true;
    private bool   _isUpdateAvailable = false;
    private bool   _isUpToDate        = false;
    private bool   _isDownloading     = false;
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
    public double DownloadProgress
    {
        get => _downloadProgress;
        private set => SetProperty(ref _downloadProgress, value);
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
    public bool IsDownloading
    {
        get => _isDownloading;
        private set => SetProperty(ref _isDownloading, value);
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

    public UpdateViewModel(IUpdateService svc)
    {
        _svc = svc;
        var v = svc.GetCurrentVersion();
        CurrentVersion = $"v{v.Major}.{v.Minor}.{v.Build}";
        _ = CheckAsync();
    }

    private async Task CheckAsync()
    {
        IsChecking        = true;
        HasError          = false;
        IsUpdateAvailable = false;
        IsUpToDate        = false;
        StatusMessage     = "Comprobando actualizaciones…";

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
            _downloadUrl  = release.Assets.Count > 0
                ? release.Assets[0].BrowserDownloadUrl
                : null;

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

    public AsyncRelayCommand DownloadCommand => new(
        async () =>
        {
            if (_downloadUrl is null) return;
            IsDownloading  = true;
            DownloadProgress = 0;
            HasError       = false;
            try
            {
                var progress = new Progress<double>(v => DownloadProgress = v);
                await _svc.DownloadAndInstallAsync(_downloadUrl, progress);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al descargar: {ex.Message}";
                HasError      = true;
                IsDownloading = false;
            }
        },
        () => IsUpdateAvailable && _downloadUrl is not null && !IsDownloading);

    public RelayCommand CheckAgainCommand => new(
        () => _ = CheckAsync(),
        () => !IsChecking && !IsDownloading);
}
