using System.Windows.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class FavoriteSlotViewModel : BaseViewModel
{
    private readonly int               _slotIndex;
    private readonly ProfileService    _profileService;
    private readonly LibraryService    _libraryService;
    private readonly ICoverService     _coverService;
    private readonly IDialogService    _dialog;
    private readonly INavigationService _nav;

    private Manga?        _manga;
    private BitmapSource? _cover;

    public FavoriteSlotViewModel(
        int slotIndex, Manga? manga,
        ProfileService profile, LibraryService library,
        ICoverService cover, IDialogService dialog, INavigationService nav)
    {
        _slotIndex      = slotIndex;
        _profileService = profile;
        _libraryService = library;
        _coverService   = cover;
        _dialog         = dialog;
        _nav            = nav;
        _manga          = manga;

        if (_manga is not null)
            _ = LoadCoverAsync();
    }

    public bool IsEmpty => _manga is null;
    public string? MangaTitle => _manga?.Title;

    public BitmapSource? Cover
    {
        get => _cover;
        private set => SetProperty(ref _cover, value);
    }

    public RelayCommand PinCommand => new(async () =>
    {
        // Show manga picker — a minimal dialog listing all known mangas
        _dialog.ShowModal(new MangaPickerViewModel(
            _libraryService.GetCollections()
                .SelectMany(c => _libraryService.GetMangasInCollection(c.Id))
                .Distinct()
                .ToList(),
            async manga =>
            {
                _manga = manga;
                await _profileService.PinFavoriteAsync(_slotIndex, manga?.Id);
                Cover = manga is not null ? await _coverService.GetCoverAsync(manga) : null;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(MangaTitle));
                _dialog.CloseModal();
            },
            _dialog));
    });

    public RelayCommand OpenCommand => new(() =>
    {
        if (_manga is null) return;
        var progress  = _libraryService.GetProgress(_manga.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(_manga, startPage));
    }, () => !IsEmpty);

    private async Task LoadCoverAsync()
    {
        if (_manga is null) return;
        Cover = await _coverService.GetCoverAsync(_manga);
    }
}
