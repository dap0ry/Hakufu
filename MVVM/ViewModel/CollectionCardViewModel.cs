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

    public ObservableCollection<Bitmap> CoverPreviews { get; } = [];

    // Las tres capas del "montón" de portadas de la tarjeta. En WPF se
    // elegían con DataTriggers sobre CoverPreviews.Count; Avalonia no tiene
    // DataTrigger, así que se calculan aquí.
    public Bitmap? FrontCover  => CoverPreviews.Count > 0 ? CoverPreviews[0] : null;
    public Bitmap? MiddleCover => CoverPreviews.Count switch { >= 2 => CoverPreviews[1], 1 => CoverPreviews[0], _ => null };
    public Bitmap? BackCover   => CoverPreviews.Count switch { >= 3 => CoverPreviews[2], 2 => CoverPreviews[1], _ => null };

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
            OnPropertyChanged(nameof(FrontCover));
            OnPropertyChanged(nameof(MiddleCover));
            OnPropertyChanged(nameof(BackCover));
        };
    }

    public async Task LoadCoversAsync(LibraryService library, ICoverService coverService)
    {
        _library = library;
        CoverPreviews.Clear();

        // Ordenadas igual que al abrir la colección, para que la portada
        // mostrada aquí sea siempre el primer manga "por orden" del usuario.
        var mangas = library.GetMangasInCollectionSorted(Model.Id)
            .Take(3)
            .ToList();

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
