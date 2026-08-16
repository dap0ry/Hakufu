using System.Windows.Media.Imaging;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class HomeViewModel : BaseViewModel
{
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly INavigationService _nav;
    private readonly ISessionService    _session;
    private readonly HakufuApiClient    _api;
    private readonly IDataRepository    _repo;

    private string?       _lastMangaTitle;
    private BitmapSource? _lastMangaCover;

    public string?       LastMangaTitle { get => _lastMangaTitle; private set => SetProperty(ref _lastMangaTitle, value); }
    public BitmapSource? LastMangaCover { get => _lastMangaCover; private set => SetProperty(ref _lastMangaCover, value); }
    public bool HasLastManga => LastMangaTitle is not null;

    public bool    IsLoggedIn     => _session.IsLoggedIn;
    public string  SessionLabel   => _session.IsLoggedIn ? _session.Username! : "Cuenta";
    public string? AvatarUrl      => _session.AvatarUrl;
    public bool    HasAvatar      => !string.IsNullOrEmpty(_session.AvatarUrl);
    public bool    ShowAvatarArea => _session.IsLoggedIn;
    public string  SessionInitial => _session.Username?.Length > 0 ? _session.Username[0].ToString().ToUpper() : "?";

    // ── Personalización (100% local, ver HomeCustomization) ─────────────────
    // Se exponen los CustomizationImage completos (Path + Opacity) — HomeView
    // se engancha a las sub-propiedades directamente (p. ej. "LibraryIcon.Path").
    public CustomizationImage? LeftPanelBackground => _repo.Current.Customization.LeftPanelBackground;

    public CustomizationImage? LibraryIcon  => IconFor("library");
    public CustomizationImage? ProfileIcon  => IconFor("profile");
    public CustomizationImage? FriendsIcon  => IconFor("friends");
    public CustomizationImage? SettingsIcon => IconFor("settings");
    public CustomizationImage? HelpIcon     => IconFor("help");
    public CustomizationImage? AccountIcon  => IconFor("account");

    public CustomizationImage? LibraryBackground  => BackgroundFor("library");
    public CustomizationImage? ProfileBackground  => BackgroundFor("profile");
    public CustomizationImage? FriendsBackground  => BackgroundFor("friends");
    public CustomizationImage? SettingsBackground => BackgroundFor("settings");
    public CustomizationImage? HelpBackground     => BackgroundFor("help");
    public CustomizationImage? AccountBackground  => BackgroundFor("account");

    private CustomizationImage? IconFor(string key)
        => _repo.Current.Customization.NavIcons.TryGetValue(key, out var img) ? img : null;

    private CustomizationImage? BackgroundFor(string key)
        => _repo.Current.Customization.NavBackgrounds.TryGetValue(key, out var img) ? img : null;

    public HomeViewModel(LibraryService library, ICoverService cover, INavigationService nav,
                         ISessionService session, HakufuApiClient api, IDataRepository repo)
    {
        _library = library;
        _cover   = cover;
        _nav     = nav;
        _session = session;
        _api     = api;
        _repo    = repo;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var last = _library.GetLastReadManga();
        if (last is not null)
        {
            LastMangaTitle = last.Title;
            LastMangaCover = await _cover.GetCoverAsync(last);
            OnPropertyChanged(nameof(HasLastManga));
        }

        if (_session.IsLoggedIn && string.IsNullOrEmpty(_session.AvatarUrl))
        {
            try
            {
                var profile = await _api.GetPublicProfileAsync(_session.Username!);
                if (!string.IsNullOrEmpty(profile?.AvatarUrl))
                {
                    _session.SetSession(_session.Username!, _session.Token!, profile.AvatarUrl);
                    OnPropertyChanged(nameof(AvatarUrl));
                    OnPropertyChanged(nameof(HasAvatar));
                }
            }
            catch { }
        }
    }

    public RelayCommand NavLibraryCommand  => new(() => _nav.NavigateTo<LibraryViewModel>());
    public RelayCommand NavProfileCommand  => new(() => _nav.NavigateTo<ProfileViewModel>());
    public RelayCommand NavSettingsCommand => new(() => _nav.NavigateTo<SettingsViewModel>());
    public RelayCommand NavHelpCommand     => new(() => _nav.NavigateTo<HelpViewModel>());
    public RelayCommand NavAccountCommand  => new(() =>
    {
        if (_session.IsLoggedIn)
            _nav.NavigateTo<SyncViewModel>();
        else
            _nav.NavigateTo<AccountViewModel>();
    });

    public RelayCommand NavFriendsCommand => new(() => _nav.NavigateTo<FriendsViewModel>());

    public RelayCommand ContinueReadingCommand => new(() =>
    {
        var last = _library.GetLastReadManga();
        if (last is null) return;
        var progress  = _library.GetProgress(last.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(last, startPage));
    }, () => HasLastManga);
}
