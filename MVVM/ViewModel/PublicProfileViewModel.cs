using System.Collections.ObjectModel;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class PublicProfileViewModel : BaseViewModel
{
    private readonly HakufuApiClient    _api;
    private readonly INavigationService _nav;

    public string ProfileUsername { get; }

    private string  _bio         = "";
    private int     _mangasCount;
    private string  _totalUsage  = "0 min";
    private string? _currentlyReadingTitle;
    private string? _currentlyReadingCover;
    private int     _currentPage;
    private int     _totalPages;
    private string? _lastCompletedTitle;
    private string? _lastCompletedCover;
    private bool    _isLoading = true;
    private string? _errorMessage;
    private bool    _isHistoryExpanded;

    public string  Bio                   { get => _bio;                   private set => SetProperty(ref _bio, value); }
    public int     MangasCount           { get => _mangasCount;           private set => SetProperty(ref _mangasCount, value); }
    public string  TotalUsage            { get => _totalUsage;            private set => SetProperty(ref _totalUsage, value); }
    public string? CurrentlyReadingTitle { get => _currentlyReadingTitle; private set => SetProperty(ref _currentlyReadingTitle, value); }
    public string? CurrentlyReadingCover { get => _currentlyReadingCover; private set => SetProperty(ref _currentlyReadingCover, value); }
    public int     CurrentPage           { get => _currentPage;           private set => SetProperty(ref _currentPage, value); }
    public int     TotalPages            { get => _totalPages;            private set => SetProperty(ref _totalPages, value); }
    public string? LastCompletedTitle    { get => _lastCompletedTitle;    private set => SetProperty(ref _lastCompletedTitle, value); }
    public string? LastCompletedCover    { get => _lastCompletedCover;    private set => SetProperty(ref _lastCompletedCover, value); }
    public bool    IsLoading             { get => _isLoading;             private set => SetProperty(ref _isLoading, value); }
    public string? ErrorMessage          { get => _errorMessage;          private set => SetProperty(ref _errorMessage, value); }

    public bool IsHistoryExpanded
    {
        get => _isHistoryExpanded;
        private set { SetProperty(ref _isHistoryExpanded, value); OnPropertyChanged(nameof(ToggleLabel)); }
    }

    public bool HasCurrentlyReading => CurrentlyReadingTitle is not null;
    public bool HasLastCompleted    => LastCompletedTitle is not null;
    public bool HasReadingSection   => HasCurrentlyReading || HasLastCompleted;
    public bool HasHistory          => RecentHistory.Count > 0;
    public bool HasFullHistory      => FullHistory.Count > 0;
    public string ReadingProgress   => TotalPages > 0 ? $"Pág. {CurrentPage} / {TotalPages}" : $"Pág. {CurrentPage}";
    public string ToggleLabel       => IsHistoryExpanded
        ? "▲ Cerrar lista completa"
        : $"▼ Ver lista completa ({FullHistory.Count} mangas)";

    public ObservableCollection<PublicHistoryItemViewModel> RecentHistory { get; } = [];
    public ObservableCollection<PublicHistoryItemViewModel> FullHistory   { get; } = [];

    public RelayCommand ToggleHistoryCommand => new(() => IsHistoryExpanded = !IsHistoryExpanded);

    public PublicProfileViewModel(string username, HakufuApiClient api, INavigationService nav)
    {
        ProfileUsername = username;
        _api            = api;
        _nav            = nav;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var profile = await _api.GetPublicProfileAsync(ProfileUsername);
            if (profile is null) { ErrorMessage = "Perfil no encontrado o es privado."; return; }

            Bio         = profile.Bio;
            MangasCount = profile.MangasCount;
            TotalUsage  = FormatDuration(TimeSpan.FromSeconds(profile.TotalUsageSeconds));

            if (profile.CurrentlyReading is { } cr)
            {
                CurrentlyReadingTitle = cr.MangaTitle;
                CurrentlyReadingCover = cr.MangaCoverUrl;
                CurrentPage           = cr.CurrentPage;
                TotalPages            = cr.TotalPages;
            }
            OnPropertyChanged(nameof(HasCurrentlyReading));
            OnPropertyChanged(nameof(ReadingProgress));

            var sorted = profile.ReadingHistory
                .OrderByDescending(h => h.CompletedAt)
                .ToList();

            if (sorted.Count > 0)
            {
                LastCompletedTitle = sorted[0].MangaTitle;
                LastCompletedCover = sorted[0].MangaCoverUrl;
            }
            OnPropertyChanged(nameof(HasLastCompleted));
            OnPropertyChanged(nameof(HasReadingSection));

            foreach (var h in sorted.Take(10))
                RecentHistory.Add(new PublicHistoryItemViewModel(h.MangaTitle, h.MangaCoverUrl));
            OnPropertyChanged(nameof(HasHistory));

            foreach (var h in sorted)
                FullHistory.Add(new PublicHistoryItemViewModel(h.MangaTitle, h.MangaCoverUrl));
            OnPropertyChanged(nameof(HasFullHistory));
            OnPropertyChanged(nameof(ToggleLabel));
        }
        catch (Exception ex) { ErrorMessage = $"Error al cargar el perfil: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private static string FormatDuration(TimeSpan t)
    {
        if (t.TotalHours >= 1)   return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes} min";
        return "< 1 min";
    }

    public RelayCommand BackCommand => new(() => _nav.NavigateTo<FriendsViewModel>());
}
