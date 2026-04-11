using System.Windows.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public record ReaderNavigationParam(Manga Manga, int StartPage);

public class ReaderViewModel : BaseViewModel, IDisposable
{
    private readonly Guid               _mangaId;
    private readonly LibraryService     _library;
    private readonly ProfileService     _profile;
    private readonly INavigationService _nav;
    private readonly IPageLoaderService _loader;

    private int           _currentPage;
    private bool          _isTwoPageMode;
    private bool          _isZenMode;
    private bool          _showZenHint;
    private BitmapSource? _pageLeft;
    private BitmapSource? _pageRight;

    /// <summary>Raised when zen mode changes; arg is true=entering, false=exiting.</summary>
    public event EventHandler<bool>? ZenModeChanged;

    public ReaderViewModel(
        Manga manga, int startPage,
        LibraryService library, ProfileService profile,
        INavigationService nav)
    {
        _mangaId    = manga.Id;
        MangaTitle  = manga.Title;
        _library    = library;
        _profile    = profile;
        _nav        = nav;
        _loader     = new PageLoaderService(manga);
        TotalPages  = _loader.TotalPages;
        _currentPage = Math.Clamp(startPage, 0, Math.Max(0, TotalPages - 1));

        _ = LoadCurrentPageAsync();
    }

    public string MangaTitle { get; }
    public int    TotalPages { get; }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (!SetProperty(ref _currentPage, value)) return;
            OnPropertyChanged(nameof(PageDisplay));
            _ = LoadCurrentPageAsync();
            _ = _library.SaveProgressAsync(_mangaId, value);

            // Mark completed when reaching last page
            if (value >= TotalPages - 1)
                _ = _profile.AddHistoryEntryAsync(_mangaId);
        }
    }

    public string PageDisplay => $"{CurrentPage + 1} / {TotalPages}";

    public bool IsTwoPageMode
    {
        get => _isTwoPageMode;
        set { if (SetProperty(ref _isTwoPageMode, value)) _ = LoadCurrentPageAsync(); }
    }

    public bool IsZenMode
    {
        get => _isZenMode;
        set
        {
            if (!SetProperty(ref _isZenMode, value)) return;
            ZenModeChanged?.Invoke(this, value);

            if (value)
            {
                ShowZenHint = true;
                var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
                _ = Task.Delay(1000).ContinueWith(_ => ShowZenHint = false, scheduler);
            }
        }
    }

    public bool ShowZenHint
    {
        get => _showZenHint;
        private set => SetProperty(ref _showZenHint, value);
    }

    public BitmapSource? PageLeft  { get => _pageLeft;  private set => SetProperty(ref _pageLeft, value); }
    public BitmapSource? PageRight { get => _pageRight; private set => SetProperty(ref _pageRight, value); }

    public RelayCommand NextPageCommand => new(
        () => CurrentPage = Math.Min(CurrentPage + (IsTwoPageMode ? 2 : 1), TotalPages - 1),
        () => CurrentPage < TotalPages - 1);

    public RelayCommand PrevPageCommand => new(
        () => CurrentPage = Math.Max(CurrentPage - (IsTwoPageMode ? 2 : 1), 0),
        () => CurrentPage > 0);

    public RelayCommand ToggleTwoPageCommand => new(() => IsTwoPageMode = !IsTwoPageMode);
    public RelayCommand ToggleZenModeCommand => new(() => IsZenMode = !IsZenMode);
    public RelayCommand ExitZenModeCommand   => new(() => { if (IsZenMode) IsZenMode = false; });

    public RelayCommand CloseReaderCommand => new(async () =>
    {
        await _library.SaveProgressAsync(_mangaId, _currentPage);
        _nav.NavigateTo<HomeViewModel>();
    });

    private async Task LoadCurrentPageAsync()
    {
        PageLeft  = await _loader.LoadPageAsync(_currentPage);
        PageRight = IsTwoPageMode ? await _loader.LoadPageAsync(_currentPage + 1) : null;
        _loader.Preload(_currentPage);
    }

    public void Dispose() => _loader.Dispose();
}
