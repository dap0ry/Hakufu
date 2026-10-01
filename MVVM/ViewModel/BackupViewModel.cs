using System.Collections.ObjectModel;
using Hakufu.Data;
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
    public string SummaryText  =>
        $"{CollectionCount} colección{(CollectionCount != 1 ? "es" : "")} · {MangaCount} tomo{(MangaCount != 1 ? "s" : "")}";

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
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }

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
            return n == 0 ? "Solo tu perfil" : $"Tu perfil y {n} de {CollectionOptions.Count} {(CollectionOptions.Count == 1 ? "colección" : "colecciones")}";
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
            var option = new BackupCollectionOption(c.Id, c.Name, c.MangaIds.Count);
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
            "Guardar copia de seguridad",
            $"Hakufu-{DateTime.Now:yyyy-MM-dd}.zip",
            FileFilter.Backup);
        if (path is null) return;

        var options = CurrentOptions();
        var what = ExportSummary;
        await RunAsync(async p =>
        {
            await _backup.ExportAsync(path, options, p);
            return (true, $"Copia guardada ({what.ToLowerInvariant()}) en {path}");
        }, "No se pudo crear la copia");
    }, () => IsIdle);

    public AsyncRelayCommand PickRestoreCommand => new(async () =>
    {
        var files = await _files.PickFilesAsync("Restaurar copia de seguridad", FileFilter.Backup, multiSelect: false);
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
            if (!result.Ok) return (false, "Ese archivo no es una copia de seguridad de Hakufu. No se ha cambiado nada.");
            await _scanner.ScanAsync();
            // El tema viene con la copia: aplicarlo ya.
            _theme.SetTheme(_repo.Current.ActiveTheme == "Dark" ? AppTheme.Dark : AppTheme.Light);
            OnPropertyChanged(nameof(MangaCount));
            OnPropertyChanged(nameof(CollectionCount));
            OnPropertyChanged(nameof(SummaryText));
            LoadCollectionOptions();
            if (result.Legacy) return (true, "Copia restaurada.");
            var text = result.Applied == 1 ? "1 tomo" : $"{result.Applied} tomos";
            return (true, result.Missing == 0
                ? $"Copia restaurada: tu perfil y el progreso de {text}."
                : $"Copia restaurada: tu perfil y el progreso de {text}. {result.Missing} de la copia no están en tu carpeta de la biblioteca.");
        }, "No se pudo restaurar la copia");
    });

    public RelayCommand OpenDataFolderCommand => new(() =>
    {
        Directory.CreateDirectory(AppPaths.DataDir); // la de Hakufu: puede no existir aún
        _files.OpenFolder(AppPaths.DataDir);
    });

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    private async Task RunAsync(Func<IProgress<double>, Task<(bool ok, string message)>> work, string errorPrefix)
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
            StatusMessage = $"{errorPrefix}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
