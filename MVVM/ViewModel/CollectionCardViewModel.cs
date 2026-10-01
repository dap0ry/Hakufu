using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class CollectionCardViewModel : BaseViewModel
{
    public Collection Model { get; }

    public string Name       => Model.Name;
    public int    MangaCount => Model.MangaIds.Count;
    public string CountText  => MangaCount == 1 ? "1 tomo" : $"{MangaCount} tomos";

    private double _progressPct;
    /// <summary>Páginas leídas sobre el total de la colección (0–100).</summary>
    public double ProgressPct { get => _progressPct; private set => SetProperty(ref _progressPct, value); }
    public bool   HasProgress => ProgressPct > 0;

    public ObservableCollection<Bitmap> CoverPreviews { get; } = [];

    // El montón de la tarjeta, como fotos apiladas: hasta 4 portadas, una por
    // tomo (sin repetir). Cover1 es la de delante.
    public const int MaxStack = 4;
    public Bitmap? Cover1 => CoverAt(0);
    public Bitmap? Cover2 => CoverAt(1);
    public Bitmap? Cover3 => CoverAt(2);
    public Bitmap? Cover4 => CoverAt(3);
    private Bitmap? CoverAt(int i) => CoverPreviews.Count > i ? CoverPreviews[i] : null;

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private bool _isFavorite;
    public bool IsFavorite { get => _isFavorite; private set => SetProperty(ref _isFavorite, value); }

    private LibraryService? _library;

    public CollectionCardViewModel(Collection collection)
    {
        Model       = collection;
        _isFavorite = collection.IsFavorite;
        CoverPreviews.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Cover1));
            OnPropertyChanged(nameof(Cover2));
            OnPropertyChanged(nameof(Cover3));
            OnPropertyChanged(nameof(Cover4));
        };
    }

    public async Task LoadCoversAsync(LibraryService library, ICoverService coverService)
    {
        _library = library;
        CoverPreviews.Clear();

        // Ordenadas igual que al abrir la colección, para que la portada
        // mostrada aquí sea siempre el primer manga "por orden" del usuario.
        var all = library.GetMangasInCollectionSorted(Model.Id);
        var totalPages = all.Sum(m => m.TotalPages);
        var readPages  = all.Sum(m => Math.Min(m.TotalPages, library.GetProgress(m.Id)?.CurrentPage + 1 ?? 0));
        ProgressPct = totalPages > 0 ? readPages * 100.0 / totalPages : 0;
        OnPropertyChanged(nameof(HasProgress));

        var mangas = all.Take(MaxStack).ToList();

        foreach (var manga in mangas)
        {
            var cover = await coverService.GetCoverAsync(manga);
            if (cover is not null)
                CoverPreviews.Add(cover);
        }
    }

    public RelayCommand ToggleFavoriteCommand => new(async () =>
    {
        if (_library is null) return;
        await _library.ToggleCollectionFavoriteAsync(Model.Id);
        IsFavorite = Model.IsFavorite;
    });
}
