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

    public UpdateViewModel(IUpdateService svc, INavigationService nav)
    {
        _svc = svc;
        _nav = nav;
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
            // El .zip del build (no un futuro checksum u otro artefacto suelto)
            // es lo único que el actualizador automático sabe instalar.
            _downloadUrl  = release.Assets
                .FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl
                ?? release.Assets.FirstOrDefault()?.BrowserDownloadUrl;

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

    // Actualización automática: descarga el zip, cierra Hakufu, reemplaza los
    // archivos y vuelve a abrirlo — sin pasos manuales. Si algo falla (p. ej.
    // updater.exe no está junto al ejecutable, o no hay red) se cae al enlace
    // manual de GitHub en vez de dejar la app a medias.
    public AsyncRelayCommand UpdateNowCommand => new(async () =>
    {
        if (string.IsNullOrEmpty(_downloadUrl))
        {
            StatusMessage = "No se encontró el archivo de la última versión.";
            HasError      = true;
            return;
        }

        IsDownloading    = true;
        HasError         = false;
        DownloadProgress = 0;
        StatusMessage    = "Descargando actualización…";

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
                StatusMessage    = $"Descargando actualización… {p:0}%";
            });
            // Si todo va bien, esto cierra Hakufu por dentro y no vuelve de aquí.
            await _svc.DownloadAndInstallAsync(_downloadUrl, progress);
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            HasError      = true;
            StatusMessage = $"No se pudo actualizar sola: {ex.Message}. Descárgala manualmente desde GitHub.";
        }
    }, () => IsUpdateAvailable && !IsDownloading);

    public RelayCommand ViewOnGitHubCommand => new(
        () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            _downloadUrl ?? "https://github.com/dap0ry/Hakufu/releases/latest")
            { UseShellExecute = true }));

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    public RelayCommand CheckAgainCommand => new(
        () => _ = CheckAsync(),
        () => !IsChecking && !IsDownloading);
}
