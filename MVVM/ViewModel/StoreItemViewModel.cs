using System.Diagnostics;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StoreItemViewModel : BaseViewModel
{
    private readonly IStoreService _store;
    private readonly CatalogItem   _item;

    // ── Datos del catálogo ────────────────────────────────────────────────
    public string Title       => _item.Title;
    public string Author      => _item.Author;
    public string Description => _item.Description;
    public string CoverUrl    => _item.CoverUrl;
    public string PagesText   => _item.Pages > 0 ? $"{_item.Pages} págs." : "";
    public string SizeText    => _item.SizeMb > 0 ? $"{_item.SizeMb:0.#} MB" : "";
    public string TagsText    => string.Join("  ·  ", _item.Tags);
    public bool   HasTags     => _item.Tags.Count > 0;
    public bool   HasMeta     => _item.Pages > 0 || _item.SizeMb > 0;
    public string MetaText
    {
        get
        {
            var parts = new List<string>();
            if (_item.Pages > 0) parts.Add($"{_item.Pages} págs.");
            if (_item.SizeMb > 0) parts.Add($"{_item.SizeMb:0.#} MB");
            return string.Join("  ·  ", parts);
        }
    }

    // ── Estado de descarga ────────────────────────────────────────────────
    private bool   _isDownloading;
    private bool   _isDownloaded;
    private double _progress;
    private string _statusText  = "";
    private string _destFolder  = "";

    public bool   IsDownloading
    {
        get => _isDownloading;
        private set
        {
            SetProperty(ref _isDownloading, value);
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(ShowItems));
        }
    }

    public bool   IsDownloaded
    {
        get => _isDownloaded;
        private set
        {
            SetProperty(ref _isDownloaded, value);
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(ShowItems));
        }
    }

    public bool   IsIdle       => !_isDownloading && !_isDownloaded;
    public bool   ShowItems    => true; // alias para bindings de visibilidad compuestos
    public double Progress     { get => _progress;    private set => SetProperty(ref _progress,    value); }
    public string StatusText   { get => _statusText;  private set => SetProperty(ref _statusText,  value); }

    // ── Comandos ──────────────────────────────────────────────────────────
    public AsyncRelayCommand DownloadCommand   { get; }
    public RelayCommand      OpenFolderCommand { get; }

    public StoreItemViewModel(CatalogItem item, IStoreService store)
    {
        _item  = item;
        _store = store;

        DownloadCommand = new AsyncRelayCommand(
            ExecuteDownloadAsync,
            () => IsIdle);

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
            _destFolder  = await _store.DownloadMangaAsync(_item.DownloadUrl, _item.Title, prog);
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
