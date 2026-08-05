using System.IO;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class BackupViewModel : BaseViewModel
{
    private readonly IGoogleDriveService _drive;
    private readonly HakufuApiClient     _api;
    private readonly INavigationService  _nav;
    private readonly IDataRepository     _repo;
    private readonly ICoverService       _cover;

    private static readonly string LibraryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu", "library");

    private bool    _isConnected;
    private bool    _isCheckingStatus = true;
    private bool    _isConfirmingBackup;
    private bool    _isConfirmingRestore;
    private bool    _isBusy;
    private string  _progressText  = "";
    private string? _statusMessage;
    private bool    _isSuccess;

    public bool IsConnected
    {
        get => _isConnected;
        private set { SetProperty(ref _isConnected, value); OnPropertyChanged(nameof(IsNotConnected)); }
    }
    public bool IsNotConnected => !_isConnected;
    public bool IsCheckingStatus { get => _isCheckingStatus; private set => SetProperty(ref _isCheckingStatus, value); }

    public bool IsConfirmingBackup  { get => _isConfirmingBackup;  private set { SetProperty(ref _isConfirmingBackup,  value); OnPropertyChanged(nameof(IsIdle)); } }
    public bool IsConfirmingRestore { get => _isConfirmingRestore; private set { SetProperty(ref _isConfirmingRestore, value); OnPropertyChanged(nameof(IsIdle)); } }
    public bool IsBusy              { get => _isBusy;              private set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(IsIdle)); OnPropertyChanged(nameof(IsNotBusy)); } }
    public bool IsIdle    => !_isBusy && !_isConfirmingBackup && !_isConfirmingRestore;
    public bool IsNotBusy => !_isBusy;

    public string  ProgressText  { get => _progressText;  private set => SetProperty(ref _progressText,  value); }
    public string? StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool    IsSuccess     { get => _isSuccess;     private set => SetProperty(ref _isSuccess,     value); }

    public BackupViewModel(IGoogleDriveService drive, HakufuApiClient api, INavigationService nav,
                           IDataRepository repo, ICoverService cover)
    {
        _drive = drive;
        _api   = api;
        _nav   = nav;
        _repo  = repo;
        _cover = cover;
        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        IsCheckingStatus = true;
        try { IsConnected = await _drive.IsConnectedAsync(); }
        catch { IsConnected = false; }
        finally { IsCheckingStatus = false; }
    }

    public AsyncRelayCommand RefreshStatusCommand => new(RefreshStatusAsync, () => !IsBusy);

    private async Task DoConnectAsync()
    {
        StatusMessage = null;
        try
        {
            await _drive.StartConnectFlowAsync();
            StatusMessage = "Completa la conexión en el navegador y pulsa \"Comprobar conexión\".";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public AsyncRelayCommand ConnectCommand => new(DoConnectAsync, () => !IsBusy);

    private async Task DoDisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await _drive.DisconnectAsync();
            IsConnected   = false;
            IsSuccess     = true;
            StatusMessage = "Google Drive desconectado.";
        }
        catch (Exception ex)
        {
            IsSuccess     = false;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    public AsyncRelayCommand DisconnectCommand => new(DoDisconnectAsync, () => !IsBusy);

    private static string MimeTypeFor(string ext) => ext switch
    {
        ".pdf" => "application/pdf",
        ".cbz" => "application/zip",
        ".cbr" => "application/vnd.rar",
        _      => "application/octet-stream",
    };

    private const string UncategorizedFolderName = "Sin colección";

    // Nombre legible para carpetas/archivos en Drive — a diferencia del Slugify
    // que usa SyncViewModel para Cloudinary (piensa en URLs), aquí queremos que
    // se vea bien al navegar el Drive a mano: se conservan mayúsculas y
    // espacios, solo se quitan los caracteres que dan problemas.
    private static string SanitizeDriveName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(text.Where(c => !invalid.Contains(c) && c != '/' && c != '\\').ToArray());
        clean = string.Join(" ", clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(clean) ? "Sin título" : clean.Trim();
    }

    private async Task DoBackupAsync()
    {
        IsBusy = true; StatusMessage = null; IsSuccess = false;
        try
        {
            ProgressText = "Conectando con Google Drive…";
            var token = await _drive.GetAccessTokenAsync();
            var rootFolderId = await _drive.FindOrCreateBackupFolderAsync(token);

            var pending = _repo.Current.Mangas
                .Where(m => string.IsNullOrEmpty(m.DriveFileId) && File.Exists(m.FilePath))
                .ToList();

            // Un manga puede estar en varias colecciones — se sube una vez y se
            // coloca en la carpeta de la primera a la que pertenezca.
            var collections = _repo.Current.Collections;
            string CollectionFolderNameFor(Manga manga)
            {
                var col = collections.FirstOrDefault(c => c.MangaIds.Contains(manga.Id));
                return col is not null ? SanitizeDriveName(col.Name) : UncategorizedFolderName;
            }

            // Cachear el id de carpeta por nombre — si hay 30 mangas en la misma
            // colección no hace falta buscar/crear esa carpeta 30 veces.
            var folderCache = new Dictionary<string, string>();
            async Task<string> GetCollectionFolderIdAsync(string name)
            {
                if (folderCache.TryGetValue(name, out var id)) return id;
                id = await _drive.FindOrCreateFolderAsync(token, name, rootFolderId);
                folderCache[name] = id;
                return id;
            }

            int total = pending.Count, current = 0;
            foreach (var manga in pending)
            {
                current++;
                var label = $"Subiendo {current} / {total} — {manga.Title}";
                ProgressText = label;
                var progress = new Progress<double>(p => ProgressText = $"{label} ({p:F0}%)");

                var collectionFolderId = await GetCollectionFolderIdAsync(CollectionFolderNameFor(manga));

                var ext = Path.GetExtension(manga.FilePath).ToLowerInvariant();
                var fileName = $"{SanitizeDriveName(manga.Title)}{ext}";
                manga.DriveFileId = await _drive.UploadFileAsync(
                    token, collectionFolderId, fileName, MimeTypeFor(ext), manga.FilePath, progress);

                // La copia de seguridad solo sube el archivo a Drive — sin esto,
                // un manga respaldado solo por aquí (sin pasar nunca por
                // "Sincronización") se queda sin CloudinaryCoverUrl, y por eso
                // no aparecía portada en el perfil público ni en el de amigos.
                if (string.IsNullOrEmpty(manga.CloudinaryCoverUrl))
                {
                    try
                    {
                        var bmp = await _cover.GetCoverAsync(manga);
                        var bytes = bmp is null ? null : await CoverUploadHelper.ToJpegAsync(bmp);
                        if (bytes is not null)
                        {
                            manga.CloudinaryCoverUrl = await _api.UploadCoverAsync(
                                CoverUploadHelper.Slugify(CollectionFolderNameFor(manga)),
                                CoverUploadHelper.Slugify(manga.Title),
                                manga.Id.ToString(), bytes);
                        }
                    }
                    catch { /* la portada es un extra — si falla, seguimos con el respaldo */ }
                }

                // Guardar tras cada archivo (no al final): si algo interrumpe la
                // subida a mitad, los archivos que sí llegaron a Drive quedan
                // enlazados localmente — sin esto, un fallo posterior (ej. al
                // subir los metadatos) perdía el DriveFileId de todo lo ya
                // subido, y un reintento lo volvía a subir duplicado.
                await _repo.SaveAsync();
            }

            ProgressText = "Subiendo metadatos de la biblioteca…";
            await _api.SyncUploadAsync(SyncPayloadBuilder.Build(_repo));

            ProgressText  = "";
            IsSuccess     = true;
            StatusMessage = total > 0
                ? $"Copia de seguridad completada. {total} archivo(s) subidos."
                : "Todo tu manga ya estaba respaldado en Drive.";
        }
        catch (Exception ex)
        {
            IsSuccess     = false;
            StatusMessage = $"Error: {ex.Message}";
            ProgressText  = "";
        }
        finally { IsBusy = false; }
    }

    private async Task DoRestoreAsync()
    {
        IsBusy = true; StatusMessage = null; IsSuccess = false;
        try
        {
            ProgressText = "Descargando metadatos del servidor…";
            var data = await _api.SyncDownloadAsync();
            if (data is null)
            {
                StatusMessage = "No hay copia de seguridad asociada a tu cuenta.";
                return;
            }

            var token = await _drive.GetAccessTokenAsync();
            Directory.CreateDirectory(LibraryDir);

            var toRestore = data.Mangas
                .Where(m => !string.IsNullOrEmpty(m.DriveFileId) && Guid.TryParse(m.Id, out _))
                .Where(m => !_repo.Current.Mangas.Any(local =>
                    local.Id == Guid.Parse(m.Id) && File.Exists(local.FilePath)))
                .ToList();

            int total = toRestore.Count, current = 0;
            foreach (var item in toRestore)
            {
                current++;
                var label = $"Descargando {current} / {total} — {item.Title}";
                ProgressText = label;
                var progress = new Progress<double>(p => ProgressText = $"{label} ({p:F0}%)");

                var remoteName = await _drive.GetFileNameAsync(token, item.DriveFileId);
                var ext        = string.IsNullOrEmpty(remoteName) ? "" : Path.GetExtension(remoteName);
                var destPath   = Path.Combine(LibraryDir, $"{item.Id}{ext}");

                await _drive.DownloadFileAsync(token, item.DriveFileId, destPath, progress);

                var id    = Guid.Parse(item.Id);
                var local = _repo.Current.Mangas.FirstOrDefault(m => m.Id == id);
                if (local is not null)
                {
                    local.FilePath = destPath;
                }
                else
                {
                    _repo.Current.Mangas.Add(new Manga
                    {
                        Id                 = id,
                        Title              = item.Title,
                        FilePath           = destPath,
                        TotalPages         = item.TotalPages,
                        DateAdded          = item.DateAdded,
                        CloudinaryCoverUrl = item.CoverCloudinaryUrl,
                        DriveFileId        = item.DriveFileId,
                    });
                }
            }

            await _repo.SaveAsync();
            ProgressText  = "";
            IsSuccess     = true;
            StatusMessage = total > 0
                ? $"Restaurados {total} archivo(s) desde Google Drive."
                : "Ya tenías localmente todo lo que hay respaldado.";
        }
        catch (Exception ex)
        {
            IsSuccess     = false;
            StatusMessage = $"Error: {ex.Message}";
            ProgressText  = "";
        }
        finally { IsBusy = false; }
    }

    public RelayCommand RequestBackupCommand  => new(() => { IsConfirmingBackup  = true; IsConfirmingRestore = false; });
    public RelayCommand RequestRestoreCommand => new(() => { IsConfirmingRestore = true; IsConfirmingBackup  = false; });
    public RelayCommand CancelCommand         => new(() => { IsConfirmingBackup  = false; IsConfirmingRestore = false; });

    public AsyncRelayCommand ConfirmBackupCommand  => new(DoBackupAsync,  () => !IsBusy);
    public AsyncRelayCommand ConfirmRestoreCommand => new(DoRestoreAsync, () => !IsBusy);

    public RelayCommand BackCommand => new(() => _nav.NavigateTo<SyncViewModel>());
}
