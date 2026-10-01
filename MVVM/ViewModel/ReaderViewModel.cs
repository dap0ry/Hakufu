using Avalonia.Media.Imaging;
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
    private Bitmap? _pageLeft;
    private Bitmap? _pageRight;

    /// <summary>Raised when zen mode changes; arg is true=entering, false=exiting.</summary>
    public event EventHandler<bool>? ZenModeChanged;

    /// <summary>
    /// Justo antes de pasar página (arg: +1 adelante, −1 atrás), con PageLeft/PageRight
    /// aún en la página vieja: la vista los usa para la animación de pasar la hoja.
    /// </summary>
    public event EventHandler<int>? PageTurning;

    /// <summary>Cuando PageLeft/PageRight ya muestran una página (arg: su índice).</summary>
    public event EventHandler<int>? PagesLoaded;

    public ReaderViewModel(
        Manga manga, int startPage,
        LibraryService library, ProfileService profile,
        INavigationService nav, ReaderSettings? settings = null)
    {
        settings ??= new ReaderSettings();
        _mangaId    = manga.Id;
        MangaTitle  = manga.Title;
        _library    = library;
        _profile    = profile;
        _nav        = nav;
        _loader     = new PageLoaderService(manga);
        TotalPages  = _loader.TotalPages;
        _currentPage = Math.Clamp(startPage, 0, Math.Max(0, TotalPages - 1));

        _isTwoPageMode       = settings.TwoPageByDefault;
        OpenInZenMode        = settings.OpenInZenMode;
        AnimatePageTurns     = settings.PageTurnAnimation;
        PageTurnSpeedFactor  = settings.PageTurnSpeed switch { "fast" => 0.6, "slow" => 1.6, _ => 1.0 };

        _ = LoadCurrentPageAsync();
    }

    public string MangaTitle { get; }

    // Ajustes → Lectura
    /// <summary>La ventana entra en zen nada más abrir el lector (lo aplica MainWindow).</summary>
    public bool   OpenInZenMode       { get; }
    public bool   AnimatePageTurns    { get; }
    /// <summary>Multiplica la duración de la animación de pasar la hoja.</summary>
    public double PageTurnSpeedFactor { get; }
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

    public Bitmap? PageLeft  { get => _pageLeft;  private set => SetProperty(ref _pageLeft, value); }
    public Bitmap? PageRight { get => _pageRight; private set => SetProperty(ref _pageRight, value); }

    public RelayCommand NextPageCommand => new(() => TurnPage(+1), () => CurrentPage < TotalPages - 1);

    public RelayCommand PrevPageCommand => new(() => TurnPage(-1), () => CurrentPage > 0);

    private void TurnPage(int direction)
    {
        var step   = IsTwoPageMode ? 2 : 1;
        var target = Math.Clamp(CurrentPage + direction * step, 0, Math.Max(0, TotalPages - 1));
        if (target == CurrentPage) return;

        PageTurning?.Invoke(this, direction);
        CurrentPage = target;
    }

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
        var page  = _currentPage;
        var left  = await _loader.LoadPageAsync(page);
        var right = IsTwoPageMode ? await _loader.LoadPageAsync(page + 1) : null;
        if (page != _currentPage) return; // ya se pidió otra página mientras cargaba

        PageLeft  = left;
        PageRight = right;
        PagesLoaded?.Invoke(this, page);
        _loader.Preload(page);
    }

    public void Dispose() => _loader.Dispose();
}
