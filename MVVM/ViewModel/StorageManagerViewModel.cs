using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StorageManagerViewModel : BaseViewModel
{
    private readonly IDialogService _dialog;
    private readonly LibraryService _library;

    private static readonly string HakufuDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu");

    public string DataPathText => HakufuDir;

    public ObservableCollection<StorageCollectionEntryViewModel> Collections { get; } = new();

    // ── Selección / borrado granular ────────────────────────────────────────

    private bool _hasSelection;
    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _showDeleteConfirm;
    public bool ShowDeleteConfirm
    {
        get => _showDeleteConfirm;
        private set => SetProperty(ref _showDeleteConfirm, value);
    }

    private string _deleteConfirmText = string.Empty;
    public string DeleteConfirmText
    {
        get => _deleteConfirmText;
        private set => SetProperty(ref _deleteConfirmText, value);
    }

    private bool _hasCollections;
    public bool HasCollections
    {
        get => _hasCollections;
        private set => SetProperty(ref _hasCollections, value);
    }

    // ── Migrate / clean state (sin cambios) ─────────────────────────────────

    private bool _showMigrateConfirm;
    public bool ShowMigrateConfirm
    {
        get => _showMigrateConfirm;
        private set => SetProperty(ref _showMigrateConfirm, value);
    }

    private bool _isMigrating;
    public bool IsMigrating
    {
        get => _isMigrating;
        private set => SetProperty(ref _isMigrating, value);
    }

    private string _migrateProgressText = string.Empty;
    public string MigrateProgressText
    {
        get => _migrateProgressText;
        private set => SetProperty(ref _migrateProgressText, value);
    }

    private int _externalCount;
    public int ExternalCount
    {
        get => _externalCount;
        private set
        {
            SetProperty(ref _externalCount, value);
            OnPropertyChanged(nameof(HasExternalMangas));
            OnPropertyChanged(nameof(MigrateHintText));
        }
    }

    public bool HasExternalMangas => _externalCount > 0;

    public string MigrateHintText =>
        _externalCount == 0
            ? "Todos los mangas ya están en la biblioteca local."
            : $"{_externalCount} archivo{(_externalCount != 1 ? "s" : "")} fuera de la biblioteca. " +
              "Se copiarán a %APPDATA%\\Hakufu\\biblioteca (los originales no se borran).";

    // ── Commands ─────────────────────────────────────────────────────────────

    public RelayCommand CloseCommand           => new(() => _dialog.CloseModal());
    public RelayCommand RequestMigrateCommand  => new(() => ShowMigrateConfirm = true,  () => HasExternalMangas && !IsMigrating);
    public RelayCommand CancelMigrateCommand   => new(() => ShowMigrateConfirm = false, () => !IsMigrating);
    public RelayCommand ConfirmMigrateCommand  => new(async () => await RunMigrationAsync());

    public RelayCommand OpenFolderCommand => new(() =>
    {
        Directory.CreateDirectory(HakufuDir);
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{HakufuDir}\"") { UseShellExecute = true }); }
        catch { /* explorer no disponible, ignorar */ }
    });

    public RelayCommand RequestDeleteCommand => new(() =>
    {
        var (collectionsCount, mangasCount) = CountSelection();
        var parts = new List<string>();
        if (collectionsCount > 0) parts.Add($"{collectionsCount} colección{(collectionsCount != 1 ? "es" : "")} entera{(collectionsCount != 1 ? "s" : "")}");
        if (mangasCount > 0)      parts.Add($"{mangasCount} tomo{(mangasCount != 1 ? "s" : "")} suelto{(mangasCount != 1 ? "s" : "")}");
        DeleteConfirmText = $"Vas a eliminar {string.Join(" y ", parts)}. Los archivos se borrarán del disco y esta acción no se puede deshacer.";
        ShowDeleteConfirm = true;
    }, () => HasSelection);

    public RelayCommand CancelDeleteCommand => new(() => ShowDeleteConfirm = false);

    public RelayCommand ConfirmDeleteCommand => new(async () =>
    {
        ShowDeleteConfirm = false;

        foreach (var col in Collections.Where(c => c.IsSelected).ToList())
            await _library.DeleteCollectionWithFilesAsync(col.Model.Id);

        foreach (var col in Collections.Where(c => !c.IsSelected).ToList())
            foreach (var manga in col.IndividuallySelectedMangas.ToList())
                await _library.DeleteMangaFromCollectionWithFileAsync(col.Model.Id, manga.Model.Id);

        LoadTree();
        ExternalCount = _library.CountExternalMangas();
    });

    // ── Constructor ──────────────────────────────────────────────────────────

    public StorageManagerViewModel(IDialogService dialog, LibraryService library)
    {
        _dialog  = dialog;
        _library = library;
        LoadTree();
        ExternalCount = _library.CountExternalMangas();
    }

    // ── Árbol colecciones → tomos ────────────────────────────────────────────

    private void LoadTree()
    {
        Collections.Clear();
        foreach (var col in _library.GetCollections())
        {
            var entry = new StorageCollectionEntryViewModel(col);
            entry.SelectionChanged += OnSelectionChanged;
            foreach (var manga in _library.GetMangasInCollectionSorted(col.Id))
                entry.AddManga(manga, GetFileSize(manga.FilePath));
            Collections.Add(entry);
        }
        HasCollections = Collections.Count > 0;
        UpdateStatus();
    }

    private static long GetFileSize(string path)
    {
        try { return !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : 0L; }
        catch { return 0L; }
    }

    private void OnSelectionChanged()
    {
        var (collectionsCount, mangasCount) = CountSelection();
        HasSelection = collectionsCount > 0 || mangasCount > 0;
        UpdateStatus();
    }

    private (int collections, int mangas) CountSelection()
    {
        int collectionsCount = Collections.Count(c => c.IsSelected);
        int mangasCount = Collections.Where(c => !c.IsSelected)
                                     .Sum(c => c.IndividuallySelectedMangas.Count());
        return (collectionsCount, mangasCount);
    }

    private void UpdateStatus()
    {
        var (collectionsCount, mangasCount) = CountSelection();
        if (collectionsCount == 0 && mangasCount == 0)
        {
            long totalBytes = Collections.Sum(c => c.Bytes);
            StatusText = $"{Collections.Count} colección{(Collections.Count != 1 ? "es" : "")}  ·  " +
                         $"Total: {StorageItemViewModel.FormatSize(totalBytes)}";
            return;
        }

        long selectedBytes = Collections.Where(c => c.IsSelected).Sum(c => c.Bytes)
                            + Collections.Where(c => !c.IsSelected)
                                         .SelectMany(c => c.IndividuallySelectedMangas)
                                         .Sum(m => m.Bytes);
        StatusText = $"{collectionsCount} colección(es) + {mangasCount} tomo(s) seleccionados  ·  " +
                     $"{StorageItemViewModel.FormatSize(selectedBytes)}";
    }

    // ── Migration ────────────────────────────────────────────────────────────

    private async Task RunMigrationAsync()
    {
        ShowMigrateConfirm = false;
        IsMigrating        = true;
        MigrateProgressText = "Iniciando...";

        var progress = new Progress<string>(file =>
            MigrateProgressText = $"Copiando: {file}");

        int moved = await _library.MigrateToLibraryAsync(progress);

        MigrateProgressText = moved == 0
            ? "No había archivos que copiar."
            : $"Listo — {moved} archivo{(moved != 1 ? "s" : "")} copiado{(moved != 1 ? "s" : "")}.";

        IsMigrating = false;
        ExternalCount = _library.CountExternalMangas();
        LoadTree();
    }
}
