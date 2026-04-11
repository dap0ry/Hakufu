using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class MangaPickerViewModel : BaseViewModel
{
    private readonly Func<Manga?, Task> _onSelected;
    private readonly IDialogService     _dialog;

    public IReadOnlyList<Manga> Mangas { get; }

    public MangaPickerViewModel(IReadOnlyList<Manga> mangas, Func<Manga?, Task> onSelected, IDialogService dialog)
    {
        Mangas      = mangas;
        _onSelected = onSelected;
        _dialog     = dialog;
    }

    public RelayCommand<Manga> SelectCommand => new(async manga =>
    {
        await _onSelected(manga);
    });

    public RelayCommand CancelCommand => new(() => _dialog.CloseModal());
}
