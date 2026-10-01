using Avalonia.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class MangaCardViewModel : BaseViewModel
{
    private readonly LibraryService? _library;

    public Manga Model { get; }

    private Bitmap? _cover;
    public Bitmap? Cover { get => _cover; private set => SetProperty(ref _cover, value); }

    public string Title      => Model.Title;
    public int    TotalPages => Model.TotalPages;

    public int CurrentPage   => _progress?.CurrentPage ?? 0;
    public double ProgressPct => TotalPages > 0 ? (double)CurrentPage / TotalPages * 100 : 0;
    public bool   IsStarted   => _progress is not null;
    public bool   IsFinished  => TotalPages > 0 && CurrentPage >= TotalPages - 1;
    /// <summary>"Pág. 12 de 180", "Terminado" o "Sin empezar" (TotalPages es 0 hasta que se sabe).</summary>
    public string PageText => IsFinished ? "Terminado"
                            : !IsStarted ? (TotalPages > 0 ? $"{TotalPages} págs." : "Sin empezar")
                            : TotalPages > 0 ? $"Pág. {CurrentPage + 1} de {TotalPages}"
                            : $"Pág. {CurrentPage + 1}";

    private bool _isFavorite;
    public bool IsFavorite { get => _isFavorite; private set => SetProperty(ref _isFavorite, value); }

    private readonly ReadingProgress? _progress;

    public MangaCardViewModel(Manga manga, ReadingProgress? progress, LibraryService? library = null)
    {
        Model       = manga;
        _progress   = progress;
        _library    = library;
        _isFavorite = manga.IsFavorite;
    }

    public async Task LoadCoverAsync(ICoverService coverService)
    {
        Cover = await coverService.GetCoverAsync(Model);
    }

    public RelayCommand ToggleFavoriteCommand => new(async () =>
    {
        if (_library is null) return;
        await _library.ToggleMangaFavoriteAsync(Model.Id);
        IsFavorite = Model.IsFavorite;
    });
}
