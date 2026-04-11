using System.Diagnostics;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StoreVolumeViewModel : BaseViewModel
{
    private readonly IStoreService _store;
    private readonly string        _mangaTitle;

    public string Label       { get; }
    public string DownloadUrl { get; }

    private bool   _isDownloading;
    private bool   _isDownloaded;
    private double _progress;
    private string _statusText = "";
    private string _destFolder = "";

    public bool   IsDownloading { get => _isDownloading; private set { SetProperty(ref _isDownloading, value); OnPropertyChanged(nameof(IsIdle)); } }
    public bool   IsDownloaded  { get => _isDownloaded;  private set { SetProperty(ref _isDownloaded,  value); OnPropertyChanged(nameof(IsIdle)); } }
    public bool   IsIdle        => !_isDownloading && !_isDownloaded;
    public double Progress      { get => _progress;   private set => SetProperty(ref _progress,   value); }
    public string StatusText    { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public AsyncRelayCommand DownloadCommand   { get; }
    public RelayCommand      OpenFolderCommand { get; }

    public StoreVolumeViewModel(CatalogVolume volume, string mangaTitle, IStoreService store)
    {
        Label       = volume.Label;
        DownloadUrl = volume.DownloadUrl;
        _mangaTitle = mangaTitle;
        _store      = store;

        DownloadCommand = new AsyncRelayCommand(
            ExecuteDownloadAsync, () => IsIdle);

        OpenFolderCommand = new RelayCommand(
            () => Process.Start(new ProcessStartInfo(_destFolder) { UseShellExecute = true }),
            () => IsDownloaded && !string.IsNullOrEmpty(_destFolder));
    }

    private async Task ExecuteDownloadAsync()
    {
        IsDownloading = true;
        Progress      = 0;
        StatusText    = "Iniciando...";

        var prog = new Progress<(double pct, string status)>(t =>
        {
            Progress   = t.pct;
            StatusText = t.status;
        });

        try
        {
            // Carpeta: descargas\{título manga}\{label}
            var folderTitle = $"{_mangaTitle} — {Label}";
            _destFolder  = await _store.DownloadMangaAsync(DownloadUrl, folderTitle, prog);
            IsDownloaded = true;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelado";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }
}
