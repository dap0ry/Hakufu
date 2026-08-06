using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class CollectionCardViewModel : BaseViewModel
{
    public Collection Model { get; }

    public string Name       => Model.Name;
    public int    MangaCount => Model.MangaIds.Count;

    public ObservableCollection<BitmapSource> CoverPreviews { get; } = [];

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private bool _isFavorite;
    public bool IsFavorite { get => _isFavorite; private set => SetProperty(ref _isFavorite, value); }

    private LibraryService? _library;

    public CollectionCardViewModel(Collection collection)
    {
        Model       = collection;
        _isFavorite = collection.IsFavorite;
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
