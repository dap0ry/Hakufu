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
    private readonly IFilePickerService _files;

    private static string HakufuDataDir => Hakufu.Data.AppPaths.DataDir;

    public SettingsViewModel(IThemeService theme, IDataRepository repo,
                             INavigationService nav, IDialogService dialog,
                             LibraryService library, IFilePickerService files)
    {
        _theme   = theme;
        _repo    = repo;
        _nav     = nav;
        _dialog  = dialog;
        _library = library;
        _files   = files;
        _isDarkTheme = _theme.CurrentTheme == AppTheme.Dark;

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
        _dialog.ShowModal(new StorageManagerViewModel(_dialog, _library, _files)));

    public string VersionText =>
        $"Versión {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?"}";

    // ── Navigation ───────────────────────────────────────────────────────────

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    // Cierra Hakufu (igual que la X de la ventana).
    public RelayCommand ExitApplicationCommand => new(() =>
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow?.Close();
    });
}
