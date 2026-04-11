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

    public CollectionCardViewModel(Collection collection)
    {
        Model = collection;
    }

    public async Task LoadCoversAsync(LibraryService library, ICoverService coverService)
    {
        CoverPreviews.Clear();
        var mangas = library.GetMangasInCollection(Model.Id)
            .Take(3)
            .ToList();

        foreach (var manga in mangas)
        {
            var cover = await coverService.GetCoverAsync(manga);
            if (cover is not null)
                CoverPreviews.Add(cover);
        }
    }
}
