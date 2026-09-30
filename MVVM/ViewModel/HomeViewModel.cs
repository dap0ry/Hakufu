using Avalonia.Media.Imaging;
using Hakufu.Data;
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

    public string? LastMangaTitle { get => _lastMangaTitle; private set => SetProperty(ref _lastMangaTitle, value); }
    public Bitmap? LastMangaCover { get => _lastMangaCover; private set => SetProperty(ref _lastMangaCover, value); }
    public bool HasLastManga => LastMangaTitle is not null;

    // ── Personalización (100% local, ver HomeCustomization) ─────────────────
    // Se exponen los CustomizationImage completos (Path + Opacity) — HomeView
    // se engancha a las sub-propiedades directamente (p. ej. "LibraryIcon.Path").
    public CustomizationImage? LeftPanelBackground => _repo.Current.Customization.LeftPanelBackground;

    // La tesela de Copia de seguridad ocupa el hueco de la antigua "Cuenta" y
    // conserva su clave ("account") para no perder imágenes ya elegidas.
    public CustomizationImage? LibraryIcon     => IconFor("library");
    public CustomizationImage? ProfileIcon     => IconFor("profile");
    public CustomizationImage? SettingsIcon    => IconFor("settings");
    public CustomizationImage? HelpIcon        => IconFor("help");
    public CustomizationImage? BackupIcon      => IconFor("account");
    public CustomizationImage? PersonalizeIcon => IconFor("personalize");

    public CustomizationImage? LibraryBackground     => BackgroundFor("library");
    public CustomizationImage? ProfileBackground     => BackgroundFor("profile");
    public CustomizationImage? SettingsBackground    => BackgroundFor("settings");
    public CustomizationImage? HelpBackground        => BackgroundFor("help");
    public CustomizationImage? BackupBackground      => BackgroundFor("account");
    public CustomizationImage? PersonalizeBackground => BackgroundFor("personalize");

    private CustomizationImage? IconFor(string key)
        => _repo.Current.Customization.NavIcons.TryGetValue(key, out var img) ? img : null;

    private CustomizationImage? BackgroundFor(string key)
        => _repo.Current.Customization.NavBackgrounds.TryGetValue(key, out var img) ? img : null;

    public HomeViewModel(LibraryService library, ICoverService cover, INavigationService nav,
                         IDataRepository repo)
    {
        _library = library;
        _cover   = cover;
        _nav     = nav;
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
    }

    public RelayCommand NavLibraryCommand   => new(() => _nav.NavigateTo<LibraryViewModel>());
    public RelayCommand NavProfileCommand   => new(() => _nav.NavigateTo<ProfileViewModel>());
    public RelayCommand NavSettingsCommand  => new(() => _nav.NavigateTo<SettingsViewModel>());
    public RelayCommand NavHelpCommand      => new(() => _nav.NavigateTo<HelpViewModel>());
    public RelayCommand NavBackupCommand    => new(() => _nav.NavigateTo<BackupViewModel>());
    public RelayCommand NavCustomizeCommand => new(() => _nav.NavigateTo<CustomizationViewModel>());

    public RelayCommand ContinueReadingCommand => new(() =>
    {
        var last = _library.GetLastReadManga();
        if (last is null) return;
        var progress  = _library.GetProgress(last.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(last, startPage));
    }, () => HasLastManga);
}
