using System.Collections.ObjectModel;
using System.IO;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StorageManagerViewModel : BaseViewModel
{
    private readonly IDialogService _dialog;
    private readonly LibraryService _library;

    private static readonly string HakufuDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu");

    public ObservableCollection<StorageItemViewModel> Items { get; } = new();

    // ── File list state ──────────────────────────────────────────────────────

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

    // ── Migrate / clean state ────────────────────────────────────────────────

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
              "Se copiarán a %APPDATA%\\Hakufu\\biblioteca y se eliminarán los originales.";

    // ── Commands ─────────────────────────────────────────────────────────────

    private readonly RelayCommand _deleteCommand;
    public RelayCommand DeleteSelectedCommand => _deleteCommand;

    public RelayCommand CloseCommand           => new(() => _dialog.CloseModal());
    public RelayCommand RequestMigrateCommand  => new(() => ShowMigrateConfirm = true,  () => HasExternalMangas && !IsMigrating);
    public RelayCommand CancelMigrateCommand   => new(() => ShowMigrateConfirm = false, () => !IsMigrating);
    public RelayCommand ConfirmMigrateCommand  => new(async () => await RunMigrationAsync());

    // ── Constructor ──────────────────────────────────────────────────────────

    public StorageManagerViewModel(IDialogService dialog, LibraryService library)
    {
        _dialog  = dialog;
        _library = library;
        _deleteCommand = new RelayCommand(ExecuteDelete, () => HasSelection);
        LoadItems();
        ExternalCount = _library.CountExternalMangas();
    }

    // ── File list ────────────────────────────────────────────────────────────

    private void LoadItems()
    {
        Items.Clear();
        if (!Directory.Exists(HakufuDir)) return;

        foreach (var file in Directory.GetFiles(HakufuDir).OrderBy(f => f))
        {
            var info = new FileInfo(file);
            AddItem(info.Name, file, info.Length);
        }

        foreach (var dir in Directory.GetDirectories(HakufuDir).OrderBy(d => d))
        {
            var size = GetDirSize(dir);
            AddItem(Path.GetFileName(dir) + "/", dir, size);
        }

        UpdateStatus();
    }

    private void AddItem(string name, string path, long bytes)
    {
        var item = new StorageItemViewModel(name, path, bytes);
        item.PropertyChanged += (_, _) => OnItemSelectionChanged();
        Items.Add(item);
    }

    private void OnItemSelectionChanged()
    {
        HasSelection = Items.Any(i => i.IsSelected);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = $"{Items.Count} elemento{(Items.Count != 1 ? "s" : "")}  ·  " +
                         $"Total: {StorageItemViewModel.FormatSize(Items.Sum(i => i.Bytes))}";
        }
        else
        {
            var bytes = selected.Sum(i => i.Bytes);
            StatusText = $"{selected.Count} seleccionado{(selected.Count != 1 ? "s" : "")}  ·  " +
                         $"{StorageItemViewModel.FormatSize(bytes)}";
        }
    }

    private void ExecuteDelete()
    {
        foreach (var item in Items.Where(i => i.IsSelected).ToList())
        {
            try
            {
                if (Directory.Exists(item.FullPath))
                    Directory.Delete(item.FullPath, recursive: true);
                else if (File.Exists(item.FullPath))
                    File.Delete(item.FullPath);

                Items.Remove(item);
            }
            catch { /* skip locked files */ }
        }

        HasSelection = false;
        UpdateStatus();
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
            ? "No había archivos que mover."
            : $"Listo — {moved} archivo{(moved != 1 ? "s" : "")} migrado{(moved != 1 ? "s" : "")}.";

        IsMigrating = false;
        ExternalCount = _library.CountExternalMangas();
        LoadItems();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static long GetDirSize(string path)
    {
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
}
