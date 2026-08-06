using System.Collections.ObjectModel;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class CollectionDetailViewModel : BaseViewModel
{
    private readonly Guid               _collectionId;
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly IDialogService     _dialog;
    private readonly INavigationService _nav;
    private readonly IFilePickerService _filePicker;

    private string _collectionName = string.Empty;
    private int    _itemsPerRow    = 6;
    private string _sortMode       = "date";

    public string CollectionName { get => _collectionName; private set => SetProperty(ref _collectionName, value); }
    public int    ItemsPerRow    { get => _itemsPerRow;    set => SetProperty(ref _itemsPerRow, value); }

    public string SortMode
    {
        get => _sortMode;
        private set
        {
            SetProperty(ref _sortMode, value);
            OnPropertyChanged(nameof(IsSortByName));
            OnPropertyChanged(nameof(IsSortByDate));
            OnPropertyChanged(nameof(IsSortByCustom));
        }
    }
    public bool IsSortByName   => SortMode == "name";
    public bool IsSortByDate   => SortMode == "date";
    public bool IsSortByCustom => SortMode == "custom";

    public ObservableCollection<MangaCardViewModel> Mangas { get; } = [];

    private bool _isSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        private set
        {
            SetProperty(ref _isSelectionMode, value);
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
    }
    public int  SelectedCount => Mangas.Count(m => m.IsSelected);
    public bool HasSelection  => SelectedCount > 0;

    public CollectionDetailViewModel(
        Guid collectionId, LibraryService library,
        ICoverService cover, IDialogService dialog,
        INavigationService nav, IFilePickerService filePicker)
    {
        _collectionId = collectionId;
        _library      = library;
        _cover        = cover;
        _dialog       = dialog;
        _nav          = nav;
        _filePicker   = filePicker;

        var col = _library.GetCollection(_collectionId);
        CollectionName = col?.Name ?? string.Empty;
        _sortMode = _library.SortMode;
        _ = LoadMangasAsync();
    }

    private async Task LoadMangasAsync()
    {
        Mangas.Clear();
        foreach (var manga in Sorted(_library.GetMangasInCollection(_collectionId)))
        {
            var vm = new MangaCardViewModel(manga, _library.GetProgress(manga.Id), _library);
            Mangas.Add(vm);
            _ = vm.LoadCoverAsync(_cover);
        }
        await Task.CompletedTask;
    }

    private IEnumerable<Manga> Sorted(IEnumerable<Manga> mangas) => LibraryService.SortMangas(mangas, SortMode);

    private async Task SetSortModeAsync(string mode)
    {
        SortMode = mode;
        _library.SortMode = mode;
        await _library.SaveAsync();
        await LoadMangasAsync();
    }

    public AsyncRelayCommand SortByNameCommand   => new(() => SetSortModeAsync("name"));
    public AsyncRelayCommand SortByDateCommand   => new(() => SetSortModeAsync("date"));

    public RelayCommand OpenReorderCommand => new(() =>
    {
        var ordered = Sorted(_library.GetMangasInCollection(_collectionId)).ToList();
        _dialog.ShowModal(new ReorderMangaViewModel(ordered, _dialog, async () =>
        {
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].CustomOrder = i;
            await SetSortModeAsync("custom");
        }));
    });

    public RelayCommand AddMangaCommand => new(async () =>
    {
        var files = _filePicker.PickFiles(
            "Agregar manga",
            "Archivos de manga|*.pdf;*.cbr;*.cbz",
            multiSelect: true);

        foreach (var file in files)
        {
            // Generate ID upfront so cover cache file uses the same ID
            var mangaId = Guid.NewGuid();
            var coverPath = await _cover.ExtractAndCacheCoverAsync(file, mangaId);
            int pages     = await GetPageCountAsync(file);
            await _library.AddMangaToCollectionAsync(_collectionId, file, pages, coverPath, mangaId);
        }
        await LoadMangasAsync();
    });

    public RelayCommand ToggleSelectionModeCommand => new(() =>
    {
        IsSelectionMode = !IsSelectionMode;
        if (!IsSelectionMode)
            foreach (var m in Mangas) m.IsSelected = false;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    });

    public RelayCommand<MangaCardViewModel> CardClickCommand => new(card =>
    {
        if (card is null) return;
        if (IsSelectionMode)
        {
            card.IsSelected = !card.IsSelected;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
        else
        {
            var progress  = _library.GetProgress(card.Model.Id);
            int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
            _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(card.Model, startPage));
        }
    });

    public RelayCommand DeleteSelectedCommand => new(async () =>
    {
        var selected = Mangas.Where(m => m.IsSelected).ToList();
        var count    = selected.Count;
        var title    = count == 1 ? "Eliminar manga" : "Eliminar mangas";
        var msg      = count == 1
            ? $"¿Eliminar \"{selected[0].Title}\" de esta colección? Esta acción no se puede deshacer."
            : $"¿Eliminar {count} mangas de esta colección? Esta acción no se puede deshacer.";

        _dialog.ShowModal(new ConfirmDeleteViewModel(title, msg, async () =>
        {
            foreach (var card in selected)
                await _library.RemoveMangaFromCollectionAsync(_collectionId, card.Model.Id);
            IsSelectionMode = false;
            await LoadMangasAsync();
        }, _dialog));
    });

    public RelayCommand<MangaCardViewModel> OpenMangaCommand => new(card =>
    {
        if (card is null) return;
        var progress  = _library.GetProgress(card.Model.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(card.Model, startPage));
    });

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<LibraryViewModel>());

    private static async Task<int> GetPageCountAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".pdf")
            {
                using var docReader = Docnet.Core.DocLib.Instance.GetDocReader(
                    filePath, new Docnet.Core.Models.PageDimensions(100, 150));
                return docReader.GetPageCount();
            }

            // CBR (RAR) and CBZ (ZIP) via SharpCompress
            var imageExts = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" };
            using var archive = SharpCompress.Archives.ArchiveFactory.OpenArchive(filePath, new SharpCompress.Readers.ReaderOptions());
            return archive.Entries.Count(e =>
                !e.IsDirectory &&
                imageExts.Contains(System.IO.Path.GetExtension(e.Key ?? "").ToLowerInvariant()));
        });
    }
}
