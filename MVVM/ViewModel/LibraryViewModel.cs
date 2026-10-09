using System.Collections.ObjectModel;
using Hakufu.Data;
using Hakufu.I18n;
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
            if (IsScanning && Collections.Count == 0) return L.Get("library.reading_folder");
            var cols  = Collections.Count;
            var tomos = Collections.Sum(c => c.MangaCount);
            return L.Format("library.summary",
                L.Format(cols == 1 ? "library.collections_one" : "library.collections_many", cols),
                CollectionCardViewModel.VolumesText(tomos));
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
    /// <summary>iPhone/iPad: los mangas se meten con la app Archivos (texto de la biblioteca vacía).</summary>
    public bool IsMobile => AppPlatform.IsMobile;
    /// <summary>Se puede elegir otra carpeta (escritorio y Android; en iOS es fija).</summary>
    public bool CanPickRoot => AppPaths.FixedLibraryRoot is null;
    /// <summary>Cómo se meten mangas cuando está vacía: iOS (app Archivos), Android (almacenamiento) o escritorio.</summary>
    public bool ShowIosEmptyHelp     => AppPaths.FixedLibraryRoot is not null;
    public bool ShowAndroidEmptyHelp => AppPlatform.IsMobile && AppPaths.FixedLibraryRoot is null;
    public bool ShowDesktopEmptyHelp => !AppPlatform.IsMobile;
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
        string? folder;
        try { folder = await _files.PickFolderAsync(L.Get("library.folder_picker_title")); }
        catch (FolderNotOnDeviceException) { ErrorText = L.Get("library.folder_not_on_device"); return; }
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
