using System.Windows.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class MangaCardViewModel : BaseViewModel
{
    public Manga Model { get; }

    private BitmapSource? _cover;
    public BitmapSource? Cover { get => _cover; private set => SetProperty(ref _cover, value); }

    public string Title      => Model.Title;
    public int    TotalPages => Model.TotalPages;

    public int CurrentPage   => _progress?.CurrentPage ?? 0;
    public double ProgressPct => TotalPages > 0 ? (double)CurrentPage / TotalPages * 100 : 0;

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private readonly ReadingProgress? _progress;

    public MangaCardViewModel(Manga manga, ReadingProgress? progress)
    {
        Model     = manga;
        _progress = progress;
    }

    public async Task LoadCoverAsync(ICoverService coverService)
    {
        Cover = await coverService.GetCoverAsync(Model);
    }
}
