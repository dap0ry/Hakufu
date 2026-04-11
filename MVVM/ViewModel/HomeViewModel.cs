using System.Windows.Media.Imaging;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class HomeViewModel : BaseViewModel
{
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly INavigationService _nav;
    private readonly IUpdateService     _updateService;

    private string?       _lastMangaTitle;
    private BitmapSource? _lastMangaCover;

    public string?       LastMangaTitle { get => _lastMangaTitle; private set => SetProperty(ref _lastMangaTitle, value); }
    public BitmapSource? LastMangaCover { get => _lastMangaCover; private set => SetProperty(ref _lastMangaCover, value); }
    public bool HasLastManga => LastMangaTitle is not null;

    public HomeViewModel(LibraryService library, ICoverService cover, INavigationService nav,
                         IUpdateService updateService)
    {
        _library       = library;
        _cover         = cover;
        _nav           = nav;
        _updateService = updateService;
        _ = LoadLastMangaAsync();
    }

    private async Task LoadLastMangaAsync()
    {
        var last = _library.GetLastReadManga();
        if (last is null) { LastMangaTitle = null; LastMangaCover = null; return; }
        LastMangaTitle = last.Title;
        LastMangaCover = await _cover.GetCoverAsync(last);
        OnPropertyChanged(nameof(HasLastManga));
    }

    public RelayCommand NavLibraryCommand  => new(() => _nav.NavigateTo<LibraryViewModel>());
    public RelayCommand NavProfileCommand  => new(() => _nav.NavigateTo<ProfileViewModel>());
    public RelayCommand NavSettingsCommand => new(() => _nav.NavigateTo<SettingsViewModel>());
    public RelayCommand NavHelpCommand     => new(() => _nav.NavigateTo<HelpViewModel>());

    public RelayCommand NavUpdatesCommand => new(() => _nav.NavigateTo<UpdateViewModel>());

    public RelayCommand ContinueReadingCommand => new(() =>
    {
        var last = _library.GetLastReadManga();
        if (last is null) return;
        var progress  = _library.GetProgress(last.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(last, startPage));
    }, () => HasLastManga);
}
