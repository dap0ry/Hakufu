using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class LibraryViewModel : BaseViewModel
{
    private readonly LibraryService  _library;
    private readonly ICoverService   _cover;
    private readonly IDialogService  _dialog;
    private readonly INavigationService _nav;

    public ObservableCollection<CollectionCardViewModel> Collections { get; } = [];

    // "Continuar leyendo" section
    private string?       _lastMangaTitle;
    private BitmapSource? _lastMangaCover;

    public string?       LastMangaTitle { get => _lastMangaTitle; private set => SetProperty(ref _lastMangaTitle, value); }
    public BitmapSource? LastMangaCover { get => _lastMangaCover; private set => SetProperty(ref _lastMangaCover, value); }
    public bool HasLastManga => LastMangaTitle is not null;

    public LibraryViewModel(LibraryService library, ICoverService cover,
                            IDialogService dialog, INavigationService nav)
    {
        _library = library;
        _cover   = cover;
        _dialog  = dialog;
        _nav     = nav;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadCollectionsAsync();
        await LoadLastMangaAsync();
    }

    public async Task LoadCollectionsAsync()
    {
        Collections.Clear();
        foreach (var col in _library.GetCollections())
        {
            var card = new CollectionCardViewModel(col);
            Collections.Add(card);
            _ = card.LoadCoversAsync(_library, _cover);
        }
        OnPropertyChanged(nameof(HasLastManga));
    }

    private async Task LoadLastMangaAsync()
    {
        var last = _library.GetLastReadManga();
        if (last is null) { LastMangaTitle = null; LastMangaCover = null; return; }
        LastMangaTitle = last.Title;
        LastMangaCover = await _cover.GetCoverAsync(last);
        OnPropertyChanged(nameof(HasLastManga));
    }

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
    public int  SelectedCount => Collections.Count(c => c.IsSelected);
    public bool HasSelection  => SelectedCount > 0;

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    public RelayCommand CreateCollectionCommand => new(() =>
    {
        _dialog.ShowModal(new CreateCollectionViewModel(
            _library, _dialog,
            onCreated: LoadCollectionsAsync));
    });

    public RelayCommand ToggleSelectionModeCommand => new(() =>
    {
        IsSelectionMode = !IsSelectionMode;
        if (!IsSelectionMode)
            foreach (var c in Collections) c.IsSelected = false;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    });

    public RelayCommand<CollectionCardViewModel> CardClickCommand => new(card =>
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
            _nav.NavigateTo<CollectionDetailViewModel>(card.Model.Id);
        }
    });

    public RelayCommand DeleteSelectedCommand => new(async () =>
    {
        var selected = Collections.Where(c => c.IsSelected).ToList();
        var count    = selected.Count;
        var title    = count == 1 ? "Eliminar colección" : "Eliminar colecciones";
        var msg      = count == 1
            ? $"¿Eliminar la colección \"{selected[0].Name}\"? Esta acción no se puede deshacer."
            : $"¿Eliminar {count} colecciones? Esta acción no se puede deshacer.";

        _dialog.ShowModal(new ConfirmDeleteViewModel(title, msg, async () =>
        {
            foreach (var card in selected)
                await _library.DeleteCollectionAsync(card.Model.Id);
            IsSelectionMode = false;
            await LoadCollectionsAsync();
        }, _dialog));
    });

    public RelayCommand ContinueReadingCommand => new(async () =>
    {
        var last = _library.GetLastReadManga();
        if (last is null) return;
        var progress = _library.GetProgress(last.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(last, startPage));
    }, () => HasLastManga);
}
