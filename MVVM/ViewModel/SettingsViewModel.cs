using System.IO;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class SettingsViewModel : BaseViewModel
{
    private readonly IThemeService      _theme;
    private readonly IDataRepository    _repo;
    private readonly INavigationService _nav;
    private readonly IDialogService     _dialog;
    private readonly LibraryService     _library;
    private readonly ICustomizationService _customization;
    private readonly IFilePickerService    _filePicker;
    private readonly IWallpaperService     _wallpaper;

    private static readonly string HakufuDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu");

    public SettingsViewModel(IThemeService theme, IDataRepository repo,
                             INavigationService nav, IDialogService dialog,
                             LibraryService library, ICustomizationService customization,
                             IFilePickerService filePicker, IWallpaperService wallpaper)
    {
        _theme         = theme;
        _repo          = repo;
        _nav           = nav;
        _dialog        = dialog;
        _library       = library;
        _customization = customization;
        _filePicker    = filePicker;
        _wallpaper     = wallpaper;
        _isDarkTheme = _theme.CurrentTheme == AppTheme.Dark;

        CustomizationSlots = BuildCustomizationSlots();

        _ = LoadStorageSizesAsync();
    }

    // ── Personalización (100% local) ─────────────────────────────────────────

    public List<ImageSlotViewModel> CustomizationSlots { get; }

    private List<ImageSlotViewModel> BuildCustomizationSlots()
    {
        var c = _repo.Current.Customization;

        ImageSlotViewModel IconSlot(string key, string label) => new(
            $"nav.{key}.icon", label, c.NavIcons.GetValueOrDefault(key),
            _customization, _filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.NavIcons.Remove(key); return; }
                var img = c.NavIcons.TryGetValue(key, out var cur) ? cur : new CustomizationImage();
                img.Path = path;
                c.NavIcons[key] = img;
            },
            onOpacityChanged: op =>
            {
                if (c.NavIcons.TryGetValue(key, out var img)) img.Opacity = op;
            },
            defaultOpacity: 1.0);

        ImageSlotViewModel BackgroundSlot(string key, string label) => new(
            $"nav.{key}.background", label, c.NavBackgrounds.GetValueOrDefault(key),
            _customization, _filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.NavBackgrounds.Remove(key); return; }
                var img = c.NavBackgrounds.TryGetValue(key, out var cur) ? cur : new CustomizationImage();
                img.Path = path;
                c.NavBackgrounds[key] = img;
            },
            onOpacityChanged: op =>
            {
                if (c.NavBackgrounds.TryGetValue(key, out var img)) img.Opacity = op;
            },
            defaultOpacity: 0.3);

        var panelSlot = new ImageSlotViewModel("panel.left", "Fondo del panel izquierdo", c.LeftPanelBackground,
            _customization, _filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.LeftPanelBackground = null; return; }
                c.LeftPanelBackground ??= new CustomizationImage();
                c.LeftPanelBackground.Path = path;
            },
            onOpacityChanged: op =>
            {
                if (c.LeftPanelBackground is not null) c.LeftPanelBackground.Opacity = op;
            },
            defaultOpacity: 0.3);

        // El wallpaper general, a diferencia de los demás huecos, también
        // tiene que aplicarse en el momento (WallpaperService sustituye el
        // recurso AppBackground del que cuelga casi toda la UI) — si no,
        // habría que salir de Ajustes y volver a entrar para verlo.
        var wallpaperSlot = new ImageSlotViewModel("wallpaper.general", "Wallpaper general (fondo de toda la app)",
            c.GeneralWallpaper, _customization, _filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.GeneralWallpaper = null; }
                else
                {
                    c.GeneralWallpaper ??= new CustomizationImage();
                    c.GeneralWallpaper.Path = path;
                }
                _wallpaper.Apply(c.GeneralWallpaper?.Path, c.GeneralWallpaper?.Opacity ?? 0.3);
            },
            onOpacityChanged: op =>
            {
                if (c.GeneralWallpaper is not null) c.GeneralWallpaper.Opacity = op;
                _wallpaper.Apply(c.GeneralWallpaper?.Path, op);
            },
            defaultOpacity: 0.3);

        return
        [
            wallpaperSlot,
            panelSlot,

            IconSlot("library", "Icono — Biblioteca"),  BackgroundSlot("library", "Fondo — Biblioteca"),
            IconSlot("profile", "Icono — Perfil"),      BackgroundSlot("profile", "Fondo — Perfil"),
            IconSlot("friends", "Icono — Amigos"),      BackgroundSlot("friends", "Fondo — Amigos"),
            IconSlot("settings", "Icono — Ajustes"),    BackgroundSlot("settings", "Fondo — Ajustes"),
            IconSlot("help", "Icono — Ayuda"),          BackgroundSlot("help", "Fondo — Ayuda"),
            IconSlot("account", "Icono — Cuenta"),      BackgroundSlot("account", "Fondo — Cuenta"),
        ];
    }

    // ── Theme ────────────────────────────────────────────────────────────────

    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (!SetProperty(ref _isDarkTheme, value)) return;
            var t = value ? AppTheme.Dark : AppTheme.Light;
            _theme.SetTheme(t);
            _repo.Current.ActiveTheme = value ? "Dark" : "Light";
            _ = _repo.SaveAsync();
        }
    }

    // ── Storage ──────────────────────────────────────────────────────────────

    private string _appSizeText = "Calculando...";
    public string AppSizeText
    {
        get => _appSizeText;
        private set => SetProperty(ref _appSizeText, value);
    }

    private string _mangasSizeText = "Calculando...";
    public string MangasSizeText
    {
        get => _mangasSizeText;
        private set => SetProperty(ref _mangasSizeText, value);
    }

    private string _cachesSizeText = "Calculando...";
    public string CachesSizeText
    {
        get => _cachesSizeText;
        private set => SetProperty(ref _cachesSizeText, value);
    }

    private async Task LoadStorageSizesAsync()
    {
        var filePaths = _repo.Current.Mangas
            .Select(m => m.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var (appBytes, dataBytes, mangaBytes) = await Task.Run(() =>
        {
            long app   = GetDirSize(AppDomain.CurrentDomain.BaseDirectory);
            long dat   = GetDirSize(HakufuDataDir);
            long manga = filePaths.Sum(p =>
            {
                try { return File.Exists(p) ? new FileInfo(p).Length : 0L; }
                catch { return 0L; }
            });
            return (app, dat, manga);
        });

        AppSizeText    = StorageItemViewModel.FormatSize(appBytes);
        MangasSizeText = StorageItemViewModel.FormatSize(mangaBytes);
        CachesSizeText = StorageItemViewModel.FormatSize(dataBytes);
    }

    private static long GetDirSize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                            .Sum(f =>
                            {
                                try { return new FileInfo(f).Length; }
                                catch { return 0L; }
                            });
        }
        catch { return 0; }
    }

    public RelayCommand OpenStorageManagerCommand => new(() =>
        _dialog.ShowModal(new StorageManagerViewModel(_dialog, _library)));

    public string VersionText =>
        $"Versión {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?"}";

    // ── Navigation ───────────────────────────────────────────────────────────

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    // Cierra Hakufu de verdad (termina el proceso) — a diferencia de la X de
    // la ventana, que solo la oculta en la bandeja del sistema.
    public RelayCommand ExitApplicationCommand => new(() =>
    {
        if (System.Windows.Application.Current.MainWindow is Hakufu.MainWindow mw)
            mw.RequestExit();
    });
}
