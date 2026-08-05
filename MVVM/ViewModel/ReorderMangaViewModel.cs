using System.Collections.ObjectModel;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

// Modal de orden personalizado: sube/baja cada manga con flechas y guarda.
// `mangas` se muta in-place al guardar — es la misma lista que ya tiene
// ordenada CollectionDetailViewModel, para que sus CustomOrder se puedan
// re-numerar justo después según el orden final que deje aquí el usuario.
public class ReorderMangaViewModel : BaseViewModel
{
    private readonly List<Manga>     _mangas;
    private readonly IDialogService  _dialog;
    private readonly Func<Task>      _onSave;

    public ObservableCollection<ReorderItemViewModel> Items { get; }

    public ReorderMangaViewModel(List<Manga> mangas, IDialogService dialog, Func<Task> onSave)
    {
        _mangas = mangas;
        _dialog = dialog;
        _onSave = onSave;
        Items   = new ObservableCollection<ReorderItemViewModel>(mangas.Select(m => new ReorderItemViewModel(m)));
    }

    public RelayCommand<ReorderItemViewModel> MoveUpCommand => new(item =>
    {
        if (item is null) return;
        var idx = Items.IndexOf(item);
        if (idx > 0) Items.Move(idx, idx - 1);
    });

    public RelayCommand<ReorderItemViewModel> MoveDownCommand => new(item =>
    {
        if (item is null) return;
        var idx = Items.IndexOf(item);
        if (idx >= 0 && idx < Items.Count - 1) Items.Move(idx, idx + 1);
    });

    public AsyncRelayCommand SaveCommand => new(async () =>
    {
        _mangas.Clear();
        _mangas.AddRange(Items.Select(i => i.Model));
        await _onSave();
        _dialog.CloseModal();
    });

    public RelayCommand CancelCommand => new(() => _dialog.CloseModal());
}

public class ReorderItemViewModel(Manga model)
{
    public Manga  Model => model;
    public string Title => model.Title;
}
