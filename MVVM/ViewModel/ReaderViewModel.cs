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

    private readonly ReaderSettings _settings;

    // Registro de lectura: el tiempo entre una acción y la siguiente cuenta
    // como leído, pero un rato muerto (lector abierto y nadie delante) no.
    private static readonly TimeSpan MaxIdle = TimeSpan.FromMinutes(5);
    private DateTime _lastActivity = DateTime.Now;

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
        _settings = settings;
        _mangaId    = manga.Id;
        MangaTitle  = manga.Title;
        _library    = library;
        _profile    = profile;
        _nav        = nav;
        _loader     = new PageLoaderService(manga);
        TotalPages  = _loader.TotalPages;
        // Un tomo recién encontrado en la carpeta puede no saber aún sus páginas.
        if (TotalPages > 0 && manga.TotalPages != TotalPages)
            _ = _library.SetTotalPagesAsync(manga.Id, TotalPages);
        _currentPage = Math.Clamp(startPage, 0, Math.Max(0, TotalPages - 1));

        _isTwoPageMode       = settings.TwoPageByDefault;
        OpenInZenMode        = settings.OpenInZenMode;
        AnimatePageTurns     = PageTurnAnimationAvailable && settings.PageTurnAnimation;
        PageTurnSpeedFactor  = settings.PageTurnSpeed switch { "fast" => 0.6, "slow" => 1.6, _ => 1.0 };

        _ = LoadCurrentPageAsync();
    }

    public string MangaTitle { get; }

    // Ajustes → Lectura
    /// <summary>La ventana entra en zen nada más abrir el lector (lo aplica MainWindow).</summary>
    public bool   OpenInZenMode       { get; }
    public bool   AnimatePageTurns    { get; }

    /// <summary>
    /// Interruptor general de la animación de pasar la hoja. Apagado de momento
    /// (01/10/2026, a petición de Dani): el código sigue ahí para recuperarla
    /// poniendo esto a true; mientras, Ajustes no enseña sus opciones.
    /// </summary>
    public static bool PageTurnAnimationAvailable { get; set; } = false;
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
        set
        {
            if (!SetProperty(ref _isTwoPageMode, value)) return;
            _ = LoadCurrentPageAsync();
        }
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

    private bool _showsTwoPages;
    /// <summary>Se ven dos páginas juntas (modo doble página, siempre emparejadas).</summary>
    public bool ShowsTwoPages { get => _showsTwoPages; private set => SetProperty(ref _showsTwoPages, value); }

    /// <summary>Página apaisada: una doble página escaneada como una sola imagen.</summary>
    public static bool IsWide(Bitmap? page) => page is not null && page.PixelSize.Width > page.PixelSize.Height;

    public RelayCommand NextPageCommand => new(async () => await TurnPageAsync(+1), () => CurrentPage < TotalPages - 1);

    public RelayCommand PrevPageCommand => new(async () => await TurnPageAsync(-1), () => CurrentPage > 0);

    private bool _turning;

    private async Task TurnPageAsync(int direction)
    {
        if (_turning) return;
        _turning = true;
        try
        {
            var step   = IsTwoPageMode ? 2 : 1;
            var target = Math.Clamp(CurrentPage + direction * step, 0, Math.Max(0, TotalPages - 1));
            if (target == CurrentPage) return;

            PageTurning?.Invoke(this, direction);
            LogActivity(pages: Math.Max(0, target - CurrentPage));
            CurrentPage = target; // guarda el progreso, y con él el registro de lectura
            await Task.CompletedTask;
        }
        finally { _turning = false; }
    }

    public RelayCommand ToggleTwoPageCommand => new(() => IsTwoPageMode = !IsTwoPageMode);
    public RelayCommand SinglePageCommand    => new(() => IsTwoPageMode = false);
    public RelayCommand TwoPagesCommand      => new(() => IsTwoPageMode = true);

    /// <summary>El comando que lanza esta tecla según los atajos de Ajustes, o null.</summary>
    public System.Windows.Input.ICommand? CommandForKey(Avalonia.Input.KeyEventArgs e)
        => ShortcutService.Match(_settings, e) switch
        {
            "next"        => NextPageCommand,
            "prev"        => PrevPageCommand,
            "toggleZen"   => ToggleZenModeCommand,
            "exitZen"     => ExitZenModeCommand,
            "singlePage"  => SinglePageCommand,
            "twoPages"    => TwoPagesCommand,
            "closeReader" => CloseReaderCommand,
            _             => null,
        };
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

        ShowsTwoPages = IsTwoPageMode;
        PageLeft  = left;
        PageRight = right;
        PagesLoaded?.Invoke(this, page);
        _loader.Preload(page);
    }

    private void LogActivity(int pages)
    {
        var now   = DateTime.Now;
        var spent = now - _lastActivity;
        _lastActivity = now;
        _profile.LogReading(spent > MaxIdle ? MaxIdle : spent, pages);
    }

    /// <summary>Apunta el tiempo desde la última página (al salir del lector o cerrar la app).</summary>
    public void FlushReadingTime()
    {
        LogActivity(pages: 0);
        _ = _profile.SaveAsync();
    }

    public void Dispose()
    {
        FlushReadingTime();
        _loader.Dispose();
    }
}
