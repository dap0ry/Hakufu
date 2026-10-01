using System.Collections.ObjectModel;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>
/// Biblioteca: las colecciones son las subcarpetas de la carpeta que eligió el
/// usuario (ver LibraryScanner). Se vuelve a leer la carpeta al entrar.
/// </summary>
public class LibraryViewModel : BaseViewModel
{
    private readonly LibraryService     _library;
    private readonly LibraryScanner     _scanner;
    private readonly ICoverService      _cover;
    private readonly INavigationService _nav;
    private readonly IFilePickerService _files;

    public ObservableCollection<CollectionCardViewModel> Collections { get; } = [];

    /// <summary>"6 colecciones · 14 tomos"</summary>
    public string SummaryText
    {
        get
        {
            if (IsScanning && Collections.Count == 0) return "Leyendo la carpeta…";
            var cols  = Collections.Count;
            var tomos = Collections.Sum(c => c.MangaCount);
            return $"{cols} {(cols == 1 ? "colección" : "colecciones")} · {tomos} {(tomos == 1 ? "tomo" : "tomos")}";
        }
    }

    private bool _hasRoot;
    private string? _errorText;
    private bool _isScanning;

    /// <summary>No hay carpeta de biblioteca: se pide elegirla.</summary>
    public bool NeedsRoot => !_hasRoot;
    /// <summary>La carpeta no se puede leer (disco desconectado, sin permiso…).</summary>
    public string? ErrorText { get => _errorText; private set { SetProperty(ref _errorText, value); RaiseState(); } }
    public bool HasError => _hasRoot && _errorText is not null;
    /// <summary>Carpeta leída y sin colecciones.</summary>
    public bool IsEmpty => _hasRoot && _errorText is null && !IsScanning && Collections.Count == 0;
    public bool IsScanning { get => _isScanning; private set { SetProperty(ref _isScanning, value); RaiseState(); } }

    public LibraryViewModel(LibraryService library, LibraryScanner scanner, ICoverService cover,
                            INavigationService nav, IFilePickerService files)
    {
        _library = library;
        _scanner = scanner;
        _cover   = cover;
        _nav     = nav;
        _files   = files;
        _hasRoot = _scanner.Root is not null;
        // Primero lo que ya se conoce (al instante) y luego lo que haya cambiado en disco.
        LoadCollections();
        _ = RefreshAsync();
    }

    private void RaiseState()
    {
        OnPropertyChanged(nameof(NeedsRoot));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void LoadCollections()
    {
        Collections.Clear();
        if (_hasRoot && _errorText is null)
            foreach (var col in _library.GetCollections())
            {
                var card = new CollectionCardViewModel(col);
                Collections.Add(card);
                _ = card.LoadCoversAsync(_library, _cover);
            }
        RaiseState();
    }

    /// <summary>Vuelve a leer la carpeta de la biblioteca y repinta.</summary>
    public Task RefreshAsync() => ShowAsync(_scanner.ScanAsync());

    private async Task ShowAsync(Task<ScanResult> scan)
    {
        IsScanning = true;
        var result = await scan;
        IsScanning = false;
        _hasRoot  = result.Status != ScanStatus.NoRoot;
        ErrorText = result.Status == ScanStatus.Unreadable ? result.Message : null;
        LoadCollections();
    }

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    public AsyncRelayCommand RefreshCommand => new(RefreshAsync, () => !IsScanning);

    public AsyncRelayCommand PickRootCommand => new(async () =>
    {
        var folder = await _files.PickFolderAsync("Carpeta de la biblioteca");
        if (folder is null) return;
        await ShowAsync(_scanner.SetRootAsync(folder));
    });

    public RelayCommand OpenFolderCommand => new(() =>
    {
        if (_scanner.Root is { } root) _files.OpenFolder(root);
    }, () => _hasRoot);

    public RelayCommand<CollectionCardViewModel> CardClickCommand => new(card =>
    {
        if (card is not null) _nav.NavigateTo<CollectionDetailViewModel>(card.Model.Id);
    });
}
