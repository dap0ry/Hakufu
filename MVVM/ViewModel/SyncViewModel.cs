using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using Hakufu.Data;
using Hakufu.Services;
using Microsoft.Win32;

namespace Hakufu.MVVM.ViewModel;

public class SyncViewModel : BaseViewModel
{
    private readonly ISessionService    _session;
    private readonly HakufuApiClient    _api;
    private readonly INavigationService _nav;
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly IDataRepository    _repo;

    private bool    _isConfirmingUpload;
    private bool    _isConfirmingDownload;
    private bool    _isBusy;
    private string  _progressText  = "";
    private string? _statusMessage;
    private bool    _isSuccess;
    private string? _avatarUrl;

    public string  Username      => _session.Username ?? "";
    public string  AvatarInitial => Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
    public string? AvatarUrl
    {
        get => _avatarUrl;
        private set { SetProperty(ref _avatarUrl, value); OnPropertyChanged(nameof(HasAvatar)); }
    }
    public bool HasAvatar => !string.IsNullOrEmpty(_avatarUrl);

    public bool IsConfirmingUpload
    {
        get => _isConfirmingUpload;
        private set { SetProperty(ref _isConfirmingUpload,   value); OnPropertyChanged(nameof(IsIdle)); }
    }
    public bool IsConfirmingDownload
    {
        get => _isConfirmingDownload;
        private set { SetProperty(ref _isConfirmingDownload, value); OnPropertyChanged(nameof(IsIdle)); }
    }
    public bool IsBusy
    {
        get => _isBusy;
        private set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(IsIdle)); OnPropertyChanged(nameof(IsNotBusy)); }
    }
    public bool IsIdle    => !_isBusy && !_isConfirmingUpload && !_isConfirmingDownload;
    public bool IsNotBusy => !_isBusy;

    public string  ProgressText  { get => _progressText;  private set => SetProperty(ref _progressText,  value); }
    public string? StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool    IsSuccess     { get => _isSuccess;     private set => SetProperty(ref _isSuccess,     value); }

    public SyncViewModel(ISessionService session, HakufuApiClient api, INavigationService nav,
                         LibraryService library, ICoverService cover, IDataRepository repo)
    {
        _session = session;
        _api     = api;
        _nav     = nav;
        _library = library;
        _cover   = cover;
        _repo    = repo;
        _ = LoadAvatarAsync();
    }

    private async Task LoadAvatarAsync()
    {
        try
        {
            var profile = await _api.GetPublicProfileAsync(_session.Username ?? "");
            if (profile is not null && !string.IsNullOrEmpty(profile.AvatarUrl))
                AvatarUrl = profile.AvatarUrl;
        }
        catch { }
    }

    private async Task DoUploadAvatarAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Imágenes (JPG, PNG, GIF)|*.jpg;*.jpeg;*.png;*.gif",
            Title  = "Seleccionar foto de perfil"
        };
        bool? picked = await Application.Current.Dispatcher.InvokeAsync(() => dlg.ShowDialog());
        if (picked != true) return;

        var info = new FileInfo(dlg.FileName);
        if (info.Length > 2 * 1024 * 1024)
        {
            IsSuccess     = false;
            StatusMessage = "La imagen no puede superar 2 MB.";
            return;
        }

        var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".gif" => "image/gif",
            ".png" => "image/png",
            _      => "image/jpeg"
        };

        IsBusy       = true;
        ProgressText = "Subiendo foto de perfil…";
        try
        {
            var bytes = await File.ReadAllBytesAsync(dlg.FileName);
            var url   = await _api.UploadAvatarAsync(bytes, contentType);
            AvatarUrl     = url;
            IsSuccess     = true;
            StatusMessage = "Foto de perfil actualizada.";
        }
        catch (Exception ex)
        {
            IsSuccess     = false;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsBusy = false; ProgressText = ""; }
    }

    public AsyncRelayCommand UploadAvatarCommand => new(DoUploadAvatarAsync, () => !IsBusy);

    private static string Slugify(string text) =>
        Regex.Replace(text.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

    private static async Task<byte[]?> ToJpegAsync(BitmapSource bmp)
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return (byte[]?)ms.ToArray();
            }
            catch { return null; }
        });
    }

    private async Task DoUploadAsync()
    {
        IsBusy        = true;
        StatusMessage = null;
        IsSuccess     = false;
        try
        {
            var collections = _repo.Current.Collections;
            var mangas      = _repo.Current.Mangas;

            int total   = mangas.Count;
            int current = 0;

            foreach (var manga in mangas)
            {
                current++;
                ProgressText = $"Portadas: {current} / {total} — {manga.Title}";
                try
                {
                    var bmp = await _cover.GetCoverAsync(manga);
                    if (bmp is null) continue;
                    var bytes = await ToJpegAsync(bmp);
                    if (bytes is null) continue;

                    // Place in collection folder; uncategorized mangas go to sin-coleccion
                    var col    = collections.FirstOrDefault(c => c.MangaIds.Contains(manga.Id));
                    var colSlug = col is not null ? Slugify(col.Name) : "sin-coleccion";

                    var url = await _api.UploadCoverAsync(
                        colSlug, Slugify(manga.Title),
                        manga.Id.ToString(), bytes);
                    manga.CloudinaryCoverUrl = url;
                }
                catch { /* skip cover, continue */ }
            }

            ProgressText = "Subiendo datos de biblioteca…";

            var history = _repo.Current.History.Select(h =>
            {
                var m = mangas.FirstOrDefault(x => x.Id == h.MangaId);
                return new HakufuApiClient.HistorySyncItem(
                    h.MangaId.ToString(),
                    m?.Title             ?? "",
                    m?.CloudinaryCoverUrl ?? "",
                    h.CompletedAt);
            }).ToList();

            var payload = new HakufuApiClient.LibrarySyncPayload(
                Mangas: mangas.Select(m => new HakufuApiClient.MangaSyncItem(
                    m.Id.ToString(), m.Title, m.TotalPages,
                    m.CloudinaryCoverUrl, m.DateAdded)).ToList(),
                Collections: collections.Select(c => new HakufuApiClient.CollectionSyncItem(
                    c.Id.ToString(), c.Name, c.Description,
                    c.MangaIds.Select(id => id.ToString()).ToList(), c.CreatedAt)).ToList(),
                ReadingProgress: _repo.Current.Progress.Select(p => new HakufuApiClient.ProgressSyncItem(
                    p.MangaId.ToString(), p.CurrentPage, p.LastRead)).ToList(),
                ReadingHistory: history,
                TotalUsageSeconds: _repo.Current.TotalUsageSeconds);

            await _api.SyncUploadAsync(payload);
            await _repo.SaveAsync();

            ProgressText  = "";
            IsSuccess     = true;
            StatusMessage = $"Datos subidos correctamente. {total} portadas procesadas.";
        }
        catch (Exception ex)
        {
            IsSuccess     = false;
            StatusMessage = $"Error: {ex.Message}";
            ProgressText  = "";
        }
        finally { IsBusy = false; }
    }

    private async Task DoDownloadAsync()
    {
        IsBusy        = true;
        StatusMessage = null;
        IsSuccess     = false;
        try
        {
            ProgressText = "Descargando datos del servidor…";
            var data = await _api.SyncDownloadAsync();
            if (data is null)
            {
                StatusMessage = "No hay datos sincronizados en el servidor.";
                return;
            }

            _repo.Current.TotalUsageSeconds = data.TotalUsageSeconds;

            foreach (var item in data.Mangas)
            {
                if (!string.IsNullOrEmpty(item.CoverCloudinaryUrl) &&
                    Guid.TryParse(item.Id, out var id))
                {
                    var local = _repo.Current.Mangas.FirstOrDefault(m => m.Id == id);
                    if (local is not null)
                        local.CloudinaryCoverUrl = item.CoverCloudinaryUrl;
                }
            }

            await _repo.SaveAsync();
            ProgressText  = "";
            IsSuccess     = true;
            StatusMessage = "Datos descargados correctamente.";
        }
        catch (Exception ex)
        {
            IsSuccess     = false;
            StatusMessage = $"Error: {ex.Message}";
            ProgressText  = "";
        }
        finally { IsBusy = false; }
    }

    public RelayCommand RequestUploadCommand   => new(() => { IsConfirmingUpload = true;  IsConfirmingDownload = false; });
    public RelayCommand RequestDownloadCommand => new(() => { IsConfirmingDownload = true; IsConfirmingUpload = false; });
    public RelayCommand CancelCommand          => new(() => { IsConfirmingUpload = false;  IsConfirmingDownload = false; });

    public AsyncRelayCommand ConfirmUploadCommand   => new(DoUploadAsync,   () => !IsBusy);
    public AsyncRelayCommand ConfirmDownloadCommand => new(DoDownloadAsync, () => !IsBusy);

    public RelayCommand BackCommand   => new(() => _nav.NavigateTo<HomeViewModel>());
    public RelayCommand LogoutCommand => new(() =>
    {
        _session.ClearSession();
        _nav.NavigateTo<HomeViewModel>();
    });
}
