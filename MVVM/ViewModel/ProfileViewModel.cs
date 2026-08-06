using System.Collections.ObjectModel;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class ProfileViewModel : BaseViewModel
{
    private readonly ProfileService     _profile;
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly IDialogService     _dialog;
    private readonly INavigationService _nav;

    public ObservableCollection<HistoryEntryViewModel> RecentActivity { get; } = [];
    public ObservableCollection<HistoryEntryViewModel> FullHistory    { get; } = [];
    public ObservableCollection<CollectionStatViewModel> CollectionStats { get; } = [];
    public ObservableCollection<CollectionCardViewModel> FavoriteCollections { get; } = [];
    public ObservableCollection<MangaCardViewModel>      TopMangas           { get; } = [];

    private bool _showingAllHistory;
    public bool ShowingAllHistory
    {
        get => _showingAllHistory;
        private set => SetProperty(ref _showingAllHistory, value);
    }

    public int    TotalPagesRead          { get; private set; }
    public string FavoriteCollectionName  { get; private set; } = "—";
    public string TotalUsageFormatted     { get; private set; } = "0 min";
    public bool   HasRecentActivity       => RecentActivity.Count > 0;
    public bool   HasCollectionStats      => CollectionStats.Count > 0;
    public bool   HasFavoriteCollections  => FavoriteCollections.Count > 0;
    public bool   HasTopMangas            => TopMangas.Count > 0;

    public ProfileViewModel(
        ProfileService profile, LibraryService library,
        ICoverService cover, IDialogService dialog, INavigationService nav)
    {
        _profile = profile;
        _library = library;
        _cover   = cover;
        _dialog  = dialog;
        _nav     = nav;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Colecciones favoritas — si se desmarca la estrella aquí mismo, desaparece de la lista
        foreach (var col in _library.GetFavoriteCollections())
        {
            var card = new CollectionCardViewModel(col);
            card.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CollectionCardViewModel.IsFavorite) && !card.IsFavorite)
                    FavoriteCollections.Remove(card);
                OnPropertyChanged(nameof(HasFavoriteCollections));
            };
            FavoriteCollections.Add(card);
            _ = card.LoadCoversAsync(_library, _cover);
        }
        OnPropertyChanged(nameof(HasFavoriteCollections));

        // Top 3 mangas favoritos (más recientes primero) — ídem
        foreach (var manga in _library.GetFavoriteMangas().Take(3))
        {
            var mangaVm = new MangaCardViewModel(manga, _library.GetProgress(manga.Id), _library);
            mangaVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MangaCardViewModel.IsFavorite) && !mangaVm.IsFavorite)
                    TopMangas.Remove(mangaVm);
                OnPropertyChanged(nameof(HasTopMangas));
            };
            TopMangas.Add(mangaVm);
            _ = mangaVm.LoadCoverAsync(_cover);
        }
        OnPropertyChanged(nameof(HasTopMangas));

        // Recently completed (last 3)
        var history = _profile.GetHistory().Take(3).ToList();
        foreach (var entry in history)
        {
            var manga = _library.GetManga(entry.MangaId);
            if (manga is null) continue;
            var vm = new HistoryEntryViewModel(manga, entry.CompletedAt);
            RecentActivity.Add(vm);
            _ = vm.LoadCoverAsync(_cover);
        }
        OnPropertyChanged(nameof(HasRecentActivity));

        // Stats
        TotalPagesRead         = _profile.GetTotalPagesRead();
        FavoriteCollectionName = _profile.GetFavoriteCollectionName();
        TotalUsageFormatted    = FormatDuration(_profile.GetTotalUsageTime());
        OnPropertyChanged(nameof(TotalPagesRead));
        OnPropertyChanged(nameof(FavoriteCollectionName));
        OnPropertyChanged(nameof(TotalUsageFormatted));

        // Collection bar chart
        var stats = _profile.GetCollectionStats();
        int maxPages = stats.Count > 0 ? stats.Max(s => s.Pages) : 1;
        foreach (var (name, pages) in stats)
            CollectionStats.Add(new CollectionStatViewModel(name, pages, maxPages));
        OnPropertyChanged(nameof(HasCollectionStats));

        await Task.CompletedTask;
    }

    private static string FormatDuration(TimeSpan t)
    {
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
        if (t.TotalMinutes >= 1)
            return $"{(int)t.TotalMinutes} min";
        return "< 1 min";
    }

    public RelayCommand GoBackCommand         => new(() => _nav.NavigateTo<HomeViewModel>());

    public RelayCommand ViewAllHistoryCommand => new(() =>
    {
        ShowingAllHistory = true;
        FullHistory.Clear();
        foreach (var entry in _profile.GetHistory())
        {
            var manga = _library.GetManga(entry.MangaId);
            if (manga is null) continue;
            var vm = new HistoryEntryViewModel(manga, entry.CompletedAt);
            FullHistory.Add(vm);
            _ = vm.LoadCoverAsync(_cover);
        }
    });

    public RelayCommand BackToProfileCommand => new(() => ShowingAllHistory = false);

    public RelayCommand<CollectionCardViewModel> OpenCollectionCommand => new(card =>
    {
        if (card is null) return;
        _nav.NavigateTo<CollectionDetailViewModel>(card.Model.Id);
    });

    public RelayCommand<MangaCardViewModel> OpenMangaCommand => new(card =>
    {
        if (card is null) return;
        var progress  = _library.GetProgress(card.Model.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(card.Model, startPage));
    });
}
