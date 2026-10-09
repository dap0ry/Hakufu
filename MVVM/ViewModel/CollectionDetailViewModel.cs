using System.Collections.ObjectModel;
using Hakufu.I18n;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class CollectionDetailViewModel : BaseViewModel, IGoBack
{
    private readonly Guid               _collectionId;
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly IDialogService     _dialog;
    private readonly INavigationService _nav;
    private readonly IFilePickerService _files;

    private string _collectionName = string.Empty;
    private int    _itemsPerRow    = 6;
    private string _sortMode       = "date";

    public string CollectionName { get => _collectionName; private set => SetProperty(ref _collectionName, value); }
    public int    ItemsPerRow    { get => _itemsPerRow;    set => SetProperty(ref _itemsPerRow, value); }
    public string Description    { get; }
    public bool   HasDescription => !string.IsNullOrWhiteSpace(Description);
    /// <summary>"3 tomos · 1 terminado"</summary>
    public string SummaryText
    {
        get
        {
            var n = Mangas.Count;
            var done = Mangas.Count(m => m.IsFinished);
            var text = CollectionCardViewModel.VolumesText(n);
            return done > 0
                ? L.Format("collection.summary", text,
                           L.Format(done == 1 ? "collection.finished_one" : "collection.finished_many", done))
                : text;
        }
    }

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

    public CollectionDetailViewModel(
        Guid collectionId, LibraryService library,
        ICoverService cover, IDialogService dialog,
        INavigationService nav, IFilePickerService files)
    {
        _collectionId = collectionId;
        _library      = library;
        _cover        = cover;
        _dialog       = dialog;
        _nav          = nav;
        _files        = files;

        var col = _library.GetCollection(_collectionId);
        CollectionName = col is null ? string.Empty : LibraryService.DisplayName(col);
        Description    = col?.Description ?? string.Empty;
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
        OnPropertyChanged(nameof(SummaryText));
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

    public RelayCommand OpenFolderCommand => new(() =>
    {
        if (_library.GetCollectionFolder(_collectionId) is { } folder) _files.OpenFolder(folder);
    });

    public RelayCommand<MangaCardViewModel> CardClickCommand => new(card =>
    {
        if (card is null) return;
        var progress  = _library.GetProgress(card.Model.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(card.Model, startPage));
    });

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<LibraryViewModel>());
}
