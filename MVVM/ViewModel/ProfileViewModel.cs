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
}
