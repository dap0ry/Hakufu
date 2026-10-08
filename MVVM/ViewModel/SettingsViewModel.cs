using System.IO;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.I18n;
using Hakufu.Services;
using System.Collections.ObjectModel;
using Avalonia.Input;

namespace Hakufu.MVVM.ViewModel;

public class SettingsViewModel : BaseViewModel
{
    private readonly IThemeService      _theme;
    private readonly IDataRepository    _repo;
    private readonly INavigationService _nav;
    private readonly LibraryScanner     _scanner;
    private readonly IFilePickerService _files;

    private static string HakufuDataDir => Hakufu.Data.AppPaths.DataDir;

    public SettingsViewModel(IThemeService theme, IDataRepository repo,
                             INavigationService nav, LibraryScanner scanner,
                             IFilePickerService files, UpdateBannerViewModel updates)
    {
        Updates  = updates;
        _theme   = theme;
        _repo    = repo;
        _nav     = nav;
        _scanner = scanner;
        _files   = files;
        _isDarkTheme = _theme.CurrentTheme == AppTheme.Dark;

        LoadShortcuts();
        _ = LoadStorageSizesAsync();
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

    // ── Carpeta de la biblioteca ─────────────────────────────────────────────

    /// <summary>Ruta de la carpeta, o un aviso si aún no hay ninguna.</summary>
    public string LibraryRootText => _scanner.Root ?? "Ninguna todavía";
    public bool   HasLibraryRoot  => _scanner.Root is not null;

    private string _libraryStatus = "";
    /// <summary>Resultado de la última lectura ("12 colecciones · 80 tomos" o el error).</summary>
    public string LibraryStatus { get => _libraryStatus; private set => SetProperty(ref _libraryStatus, value); }

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; private set => SetProperty(ref _isScanning, value); }

    public AsyncRelayCommand PickLibraryRootCommand => new(async () =>
    {
        var folder = await _files.PickFolderAsync("Carpeta de la biblioteca");
        if (folder is not null) await ScanAsync(_scanner.SetRootAsync(folder));
    }, () => !IsScanning);

    public RelayCommand OpenLibraryRootCommand => new(() =>
    {
        if (_scanner.Root is { } root) _files.OpenFolder(root);
    }, () => HasLibraryRoot);

    public AsyncRelayCommand RescanLibraryCommand => new(() => ScanAsync(_scanner.ScanAsync()),
                                                         () => HasLibraryRoot && !IsScanning);

    private async Task ScanAsync(Task<ScanResult> scan)
    {
        IsScanning = true;
        LibraryStatus = "Leyendo la carpeta…";
        var result = await scan;
        IsScanning = false;
        if (result.Ok)
        {
            var cols  = _repo.Current.Collections.Count;
            var tomos = _repo.Current.Mangas.Count;
            LibraryStatus = $"{cols} {(cols == 1 ? "colección" : "colecciones")} · {tomos} {(tomos == 1 ? "tomo" : "tomos")}";
        }
        else LibraryStatus = result.Message ?? "";
        OnPropertyChanged(nameof(LibraryRootText));
        OnPropertyChanged(nameof(HasLibraryRoot));
        _ = LoadStorageSizesAsync();
    }

    // ── Lectura ──────────────────────────────────────────────────────────────

    private ReaderSettings Reader => _repo.Current.Reader;

    private void SaveReader([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        _ = _repo.SaveAsync();
    }

    public bool TwoPageByDefault
    {
        get => Reader.TwoPageByDefault;
        set { if (Reader.TwoPageByDefault == value) return; Reader.TwoPageByDefault = value; SaveReader(); }
    }

    public bool OpenInZenMode
    {
        get => Reader.OpenInZenMode;
        set { if (Reader.OpenInZenMode == value) return; Reader.OpenInZenMode = value; SaveReader(); }
    }

    public bool PageTurnAnimation
    {
        get => Reader.PageTurnAnimation;
        set { if (Reader.PageTurnAnimation == value) return; Reader.PageTurnAnimation = value; SaveReader(); }
    }

    /// <summary>Las opciones de animación solo se enseñan si la animación existe (ver ReaderViewModel).</summary>
    public bool ShowPageTurnSettings => ReaderViewModel.PageTurnAnimationAvailable;

    public bool IsSpeedFast   => Reader.PageTurnSpeed == "fast";
    public bool IsSpeedNormal => Reader.PageTurnSpeed is not ("fast" or "slow");
    public bool IsSpeedSlow   => Reader.PageTurnSpeed == "slow";

    public RelayCommand SpeedFastCommand   => new(() => SetSpeed("fast"));
    public RelayCommand SpeedNormalCommand => new(() => SetSpeed("normal"));
    public RelayCommand SpeedSlowCommand   => new(() => SetSpeed("slow"));

    private void SetSpeed(string speed)
    {
        Reader.PageTurnSpeed = speed;
        OnPropertyChanged(nameof(IsSpeedFast));
        OnPropertyChanged(nameof(IsSpeedNormal));
        OnPropertyChanged(nameof(IsSpeedSlow));
        _ = _repo.SaveAsync();
    }

    // ── Atajos de teclado ───────────────────────────────────────────────────

    public ObservableCollection<ShortcutRowViewModel> Shortcuts { get; } = [];

    private ShortcutSlotViewModel? _capturing;
    /// <summary>El hueco que está esperando una tecla (la vista le pasa la siguiente que se pulse).</summary>
    public ShortcutSlotViewModel? CapturingSlot => _capturing;

    private string _shortcutNotice = "";
    public string ShortcutNotice { get => _shortcutNotice; private set => SetProperty(ref _shortcutNotice, value); }

    private void LoadShortcuts()
    {
        StopCapture();
        Shortcuts.Clear();
        foreach (var action in ShortcutService.All)
            Shortcuts.Add(new ShortcutRowViewModel(action, ShortcutService.GetGestures(Reader, action.Id), OnSlotClicked));
    }

    private void OnSlotClicked(ShortcutSlotViewModel slot)
    {
        var again = _capturing == slot;
        StopCapture();
        if (again) return; // segundo clic en el mismo hueco: cancelar
        _capturing = slot;
        slot.IsCapturing = true;
        ShortcutNotice = "";
    }

    private void StopCapture()
    {
        if (_capturing is not null) _capturing.IsCapturing = false;
        _capturing = null;
    }

    /// <summary>
    /// Asigna la tecla pulsada al hueco que escucha. Si otra acción ya la usaba,
    /// se la quita (cada tecla hace una sola cosa). Devuelve false si no había
    /// ningún hueco escuchando.
    /// </summary>
    public bool AssignCapturedKey(KeyGesture gesture)
    {
        var slot = _capturing;
        if (slot is null) return false;
        StopCapture();

        var notice = "";
        foreach (var other in Shortcuts.SelectMany(r => r.Slots))
            if (other != slot && other.Gesture is { } g && g.Equals(gesture))
            {
                other.Gesture = null;
                if (other.Row != slot.Row)
                    notice = $"{ShortcutService.Display(gesture)} ya no hace «{other.Row.Label}».";
                Save(other.Row);
            }

        slot.Gesture = gesture;
        Save(slot.Row);
        ShortcutNotice = notice;
        return true;

        void Save(ShortcutRowViewModel row) => ShortcutService.SetGestures(Reader, row.Action.Id, row.Gestures);
    }

    public RelayCommand<ShortcutSlotViewModel> ClearShortcutCommand => new(slot =>
    {
        if (slot is null) return;
        StopCapture();
        slot.Gesture = null;
        ShortcutService.SetGestures(Reader, slot.Row.Action.Id, slot.Row.Gestures);
        _ = _repo.SaveAsync();
    });

    public RelayCommand ResetShortcutsCommand => new(() =>
    {
        ShortcutService.ResetAll(Reader);
        LoadShortcuts();
        ShortcutNotice = "Atajos de fábrica restaurados.";
        _ = _repo.SaveAsync();
    });

    /// <summary>Guarda tras asignar una tecla (la vista llama aquí).</summary>
    public Task SaveShortcutsAsync() => _repo.SaveAsync();

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
            // La antigua carpeta "biblioteca" son mangas (puede ser la de la biblioteca), no caché.
            long dat   = GetDirSize(HakufuDataDir) - GetDirSize(AppPaths.LibraryDir);
            long manga = filePaths.Sum(p =>
            {
                try { return File.Exists(p) ? new FileInfo(p).Length : 0L; }
                catch { return 0L; }
            });
            return (app, dat, manga);
        });

        AppSizeText    = FormatSize(appBytes);
        MangasSizeText = FormatSize(mangaBytes);
        CachesSizeText = FormatSize(dataBytes);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
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

    public string VersionText =>
        $"Versión {AppVersion.Current}"; // con su -beta.N, si lo tiene

    // ── Navigation ───────────────────────────────────────────────────────────

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    // ── Idioma ───────────────────────────────────────────────────────────────

    public bool IsSpanish => Localizer.Instance.Language == "es";
    public bool IsEnglish => Localizer.Instance.Language == "en";

    public RelayCommand SpanishCommand => new(() => SetLanguage("es"));
    public RelayCommand EnglishCommand => new(() => SetLanguage("en"));

    private void SetLanguage(string lang)
    {
        if (Localizer.Instance.Language == lang) return;
        Localizer.Instance.SetLanguage(lang);
        _repo.Current.Language = lang;
        _ = _repo.SaveAsync();
        // Los textos que calculan los ViewModels se rehacen recreando la pantalla.
        _nav.Reload();
    }

    // ── Actualizaciones ──────────────────────────────────────────────────────

    /// <summary>La misma barra de MainWindow: buscar desde aquí la enseña si hay versión nueva.</summary>
    public UpdateBannerViewModel Updates { get; }

    public AsyncRelayCommand CheckUpdatesCommand => new(() => Updates.CheckNowAsync());

    public bool CheckUpdatesOnStartup
    {
        get => Updates.CheckOnStartup;
        set
        {
            if (Updates.CheckOnStartup == value) return;
            Updates.CheckOnStartup = value;
            OnPropertyChanged();
            _ = _repo.SaveAsync();
        }
    }

    public RelayCommand OpenLegalCommand => new(() => _nav.NavigateTo<LegalViewModel>());

    // Cierra Hakufu (igual que la X de la ventana).
    public RelayCommand ExitApplicationCommand => new(() =>
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow?.Close();
    });
}
