using System.Collections.ObjectModel;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class LibraryViewModel : BaseViewModel
{
    private readonly LibraryService  _library;
    private readonly ICoverService   _cover;
    private readonly IDialogService  _dialog;
    private readonly INavigationService _nav;

    public ObservableCollection<CollectionCardViewModel> Collections { get; } = [];

    /// <summary>"6 colecciones · 14 tomos"</summary>
    public string SummaryText
    {
        get
        {
            var cols  = Collections.Count;
            var tomos = Collections.Sum(c => c.MangaCount);
            return $"{cols} {(cols == 1 ? "colección" : "colecciones")} · {tomos} {(tomos == 1 ? "tomo" : "tomos")}";
        }
    }
    public bool IsEmpty => Collections.Count == 0;

    public LibraryViewModel(LibraryService library, ICoverService cover,
                            IDialogService dialog, INavigationService nav)
    {
        _library = library;
        _cover   = cover;
        _dialog  = dialog;
        _nav     = nav;
        _ = InitializeAsync();
    }

    private Task InitializeAsync() => LoadCollectionsAsync();

    public async Task LoadCollectionsAsync()
    {
        Collections.Clear();
        foreach (var col in _library.GetCollections())
        {
            var card = new CollectionCardViewModel(col);
            Collections.Add(card);
            _ = card.LoadCoversAsync(_library, _cover);
        }
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsEmpty));
        await Task.CompletedTask;
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
}
