using System.Collections.ObjectModel;
using Hakufu.Data;
using Hakufu.I18n;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>
/// Copia de seguridad 100% local: un .zip que se queda donde lo guarde el
/// usuario, con su perfil y, si quiere, las colecciones que elija (progreso,
/// favoritos…); los mangas no, son su carpeta. Al restaurar se combina.
/// </summary>
public class BackupViewModel : BaseViewModel
{
    private readonly IBackupService     _backup;
    private readonly IFilePickerService _files;
    private readonly INavigationService _nav;
    private readonly IDataRepository    _repo;
    private readonly IThemeService      _theme;
    private readonly LibraryScanner     _scanner;

    private bool    _isConfirmingRestore;
    private bool    _isBusy;
    private double  _progress;
    private string? _statusMessage;
    private bool    _isSuccess;
    private string? _pendingRestorePath;

    public BackupViewModel(IBackupService backup, IFilePickerService files, INavigationService nav,
                           IDataRepository repo, IThemeService theme, LibraryScanner scanner)
    {
        _backup    = backup;
        _files     = files;
        _nav       = nav;
        _repo      = repo;
        _theme     = theme;
        _scanner   = scanner;
        LoadCollectionOptions();
    }

    public string DataFolder => AppPaths.DataDir;

    public int MangaCount      => _repo.Current.Mangas.Count;
    public int CollectionCount => _repo.Current.Collections.Count;
    public string SummaryText  => L.Format("backup.summary", Collections(CollectionCount), Volumes(MangaCount));

    /// <summary>"1 colección" / "3 colecciones".</summary>
    internal static string Collections(int n) => n == 1 ? L.Get("backup.collections_one") : L.Format("backup.collections_other", n);
    /// <summary>"1 tomo" / "3 tomos".</summary>
    internal static string Volumes(int n) => n == 1 ? L.Get("backup.volumes_one") : L.Format("backup.volumes_other", n);

    public bool IsConfirmingRestore
    {
        get => _isConfirmingRestore;
        private set { SetProperty(ref _isConfirmingRestore, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isBusy && !_isConfirmingRestore;

    /// <summary>0–100.</summary>
    public double Progress
    {
        get => _progress;
        private set { if (SetProperty(ref _progress, value)) OnPropertyChanged(nameof(ProgressText)); }
    }
    /// <summary>"Trabajando… 40 %".</summary>
    public string ProgressText => L.Format("backup.working", Progress);

    public string? StatusMessage
    {
        get => _statusMessage;
        private set { SetProperty(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatus)); }
    }
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);
    public bool IsSuccess { get => _isSuccess; private set => SetProperty(ref _isSuccess, value); }

    // ── Commands ─────────────────────────────────────────────────────────────

    // ── Qué exportar ─────────────────────────────────────────────────────────
    // El perfil va siempre; las colecciones, si se quiere y las que se elijan.

    public ObservableCollection<BackupCollectionOption> CollectionOptions { get; } = [];

    private bool _includeCollections = true;
    public bool IncludeCollections
    {
        get => _includeCollections;
        set { if (SetProperty(ref _includeCollections, value)) OnPropertyChanged(nameof(ExportSummary)); }
    }

    public bool HasCollections => CollectionOptions.Count > 0;

    /// <summary>"Perfil y 3 de 5 colecciones" — lo que va a ir en la copia.</summary>
    public string ExportSummary
    {
        get
        {
            var n = IncludeCollections ? CollectionOptions.Count(c => c.IsSelected) : 0;
            if (n == 0) return L.Get("backup.export_summary_profile_only");
            var total = CollectionOptions.Count;
            return L.Format(total == 1 ? "backup.export_summary_one" : "backup.export_summary_other", n, total);
        }
    }

    public RelayCommand SelectAllCollectionsCommand  => new(() => SetAll(true));
    public RelayCommand SelectNoCollectionsCommand   => new(() => SetAll(false));

    private void SetAll(bool on)
    {
        foreach (var c in CollectionOptions) c.IsSelected = on;
    }

    private void LoadCollectionOptions()
    {
        CollectionOptions.Clear();
        foreach (var c in _repo.Current.Collections.OrderBy(c => c.Name, NaturalComparer.Instance))
        {
            var option = new BackupCollectionOption(c.Id, LibraryService.DisplayName(c), c.MangaIds.Count);
            option.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ExportSummary));
            CollectionOptions.Add(option);
        }
        OnPropertyChanged(nameof(HasCollections));
        OnPropertyChanged(nameof(ExportSummary));
    }

    private BackupOptions CurrentOptions() => IncludeCollections
        ? new BackupOptions(CollectionOptions.Where(c => c.IsSelected).Select(c => c.Id).ToList())
        : BackupOptions.ProfileOnly;

    public AsyncRelayCommand ExportCommand => new(async () =>
    {
        var path = await _files.SaveFileAsync(
            L.Get("backup.save_dialog_title"),
            $"Hakufu-{DateTime.Now:yyyy-MM-dd}.zip",
            FileFilter.Backup);
        if (path is null) return;

        var options = CurrentOptions();
        var what = ExportSummary;
        await RunAsync(async p =>
        {
            await _backup.ExportAsync(path, options, p);
            return (true, L.Format("backup.export_done", what.ToLower(L.Culture), FilePickerService.DisplayPath(path)));
        }, "backup.export_failed");
    }, () => IsIdle);

    public AsyncRelayCommand PickRestoreCommand => new(async () =>
    {
        var files = await _files.PickFilesAsync(L.Get("backup.restore_dialog_title"), FileFilter.Backup, multiSelect: false);
        if (files.Length == 0) return;
        _pendingRestorePath = files[0];
        StatusMessage = null;
        IsConfirmingRestore = true;
    }, () => IsIdle);

    public RelayCommand CancelRestoreCommand => new(() =>
    {
        _pendingRestorePath = null;
        IsConfirmingRestore = false;
    });

    public AsyncRelayCommand ConfirmRestoreCommand => new(async () =>
    {
        var path = _pendingRestorePath;
        IsConfirmingRestore = false;
        if (path is null) return;

        await RunAsync(async p =>
        {
            // Primero la carpeta al día: los tomos de la copia se buscan en ella.
            await _scanner.ScanAsync();
            var result = await _backup.ImportAsync(path, p);
            if (!result.Ok) return (false, L.Get("backup.restore_invalid"));
            await _scanner.ScanAsync();
            // El tema viene con la copia: aplicarlo ya.
            _theme.SetTheme(_repo.Current.ActiveTheme == "Dark" ? AppTheme.Dark : AppTheme.Light);
            OnPropertyChanged(nameof(MangaCount));
            OnPropertyChanged(nameof(CollectionCount));
            OnPropertyChanged(nameof(SummaryText));
            LoadCollectionOptions();
            if (result.Legacy) return (true, L.Get("backup.restore_done_legacy"));
            var text = Volumes(result.Applied);
            return (true, result.Missing == 0
                ? L.Format("backup.restore_done", text)
                : L.Format("backup.restore_done_missing", text, result.Missing));
        }, "backup.restore_failed");
    });

    public RelayCommand OpenDataFolderCommand => new(() =>
    {
        Directory.CreateDirectory(AppPaths.DataDir); // la de Hakufu: puede no existir aún
        _files.OpenFolder(AppPaths.DataDir);
    });

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    private async Task RunAsync(Func<IProgress<double>, Task<(bool ok, string message)>> work, string errorKey)
    {
        IsBusy = true;
        Progress = 0;
        StatusMessage = null;
        try
        {
            var progress = new Progress<double>(v => Progress = v * 100);
            var (ok, message) = await work(progress);
            IsSuccess = ok;
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            StatusMessage = L.Format(errorKey, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
