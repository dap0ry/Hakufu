using Hakufu.Data;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>
/// Copia de seguridad 100% local: exporta/importa un .zip con la biblioteca
/// (datos, portadas y perfil; los mangas no, son la carpeta del usuario).
/// Sustituye a la antigua copia en Dropbox.
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

    public AsyncRelayCommand ExportCommand => new(async () =>
    {
        var path = await _files.SaveFileAsync(
            "Guardar copia de seguridad",
            $"Hakufu-{DateTime.Now:yyyy-MM-dd}.zip",
            FileFilter.Backup);
        if (path is null) return;

        await RunAsync(async p =>
        {
            await _backup.ExportAsync(path, p);
            return (true, $"Copia guardada en {path}");
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
            var ok = await _backup.ImportAsync(path, p);
            if (!ok) return (false, "Ese archivo no es una copia de seguridad de Hakufu. No se ha cambiado nada.");

            // Los tomos de la copia se buscan en la carpeta de la biblioteca de este equipo.
            await _scanner.ScanAsync();
            // El tema viene con la copia: aplicarlo ya.
            _theme.SetTheme(_repo.Current.ActiveTheme == "Dark" ? AppTheme.Dark : AppTheme.Light);
            OnPropertyChanged(nameof(MangaCount));
            OnPropertyChanged(nameof(CollectionCount));
            OnPropertyChanged(nameof(SummaryText));
            return (true, "Copia restaurada.");
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
