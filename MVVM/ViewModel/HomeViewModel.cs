using Avalonia.Media.Imaging;
using Hakufu.Data;
using Hakufu.I18n;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class HomeViewModel : BaseViewModel
{
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly INavigationService _nav;
    private readonly IDataRepository    _repo;

    private string? _lastMangaTitle;
    private Bitmap? _lastMangaCover;
    private string? _lastMangaPageText;
    private double  _lastMangaProgress;

    public string? LastMangaTitle { get => _lastMangaTitle; private set => SetProperty(ref _lastMangaTitle, value); }
    public Bitmap? LastMangaCover { get => _lastMangaCover; private set => SetProperty(ref _lastMangaCover, value); }
    public bool HasLastManga => LastMangaTitle is not null;
    /// <summary>"Página 12 de 180" / "Page 12 of 180" (null si no se sabe cuántas páginas tiene).</summary>
    public string? LastMangaPageText { get => _lastMangaPageText; private set => SetProperty(ref _lastMangaPageText, value); }
    /// <summary>Avance de 0 a 100.</summary>
    public double LastMangaProgress { get => _lastMangaProgress; private set => SetProperty(ref _lastMangaProgress, value); }

    // Foto de perfil junto a "Perfil" en el menú (o la inicial si no hay foto).
    public Bitmap? ProfileAvatar  { get; }
    public bool    HasProfileAvatar => ProfileAvatar is not null;
    public string  ProfileInitial { get; } = "";
    public bool    ShowProfileInitial => !HasProfileAvatar && ProfileInitial.Length > 0;

    public HomeViewModel(LibraryService library, ICoverService cover, INavigationService nav,
                         IDataRepository repo)
    {
        _library = library;
        _cover   = cover;
        _nav     = nav;
        _repo    = repo;
        var profile   = repo.Current.Profile;
        ProfileAvatar = BitmapHelper.TryLoad(profile.AvatarPath);
        ProfileInitial = string.IsNullOrWhiteSpace(profile.Name) ? "" : profile.Name.Trim()[..1].ToUpperInvariant();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var last = _library.GetLastReadManga();
        if (last is not null)
        {
            LastMangaTitle = last.Title;
            var page  = (_library.GetProgress(last.Id)?.CurrentPage ?? 0) + 1;
            var total = last.TotalPages;
            if (total > 0)
            {
                page = Math.Min(page, total);
                LastMangaPageText = L.Format("home.page_of", page, total);
                LastMangaProgress = page * 100.0 / total;
            }
            LastMangaCover = await _cover.GetCoverAsync(last);
            OnPropertyChanged(nameof(HasLastManga));
        }
    }

    public RelayCommand NavLibraryCommand   => new(() => _nav.NavigateTo<LibraryViewModel>());
    public RelayCommand NavProfileCommand   => new(() => _nav.NavigateTo<ProfileViewModel>());
    public RelayCommand NavSettingsCommand  => new(() => _nav.NavigateTo<SettingsViewModel>());
    public RelayCommand NavHelpCommand      => new(() => _nav.NavigateTo<HelpViewModel>());
    public RelayCommand NavBackupCommand    => new(() => _nav.NavigateTo<BackupViewModel>());

    public RelayCommand ContinueReadingCommand => new(() =>
    {
        var last = _library.GetLastReadManga();
        if (last is null) return;
        var progress  = _library.GetProgress(last.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(last, startPage));
    }, () => HasLastManga);
}
