using Hakufu.MVVM.Model;

namespace Hakufu.MVVM.ViewModel;

/// <summary>Un tomo dentro de una colección, en el árbol de "Gestionar espacio".</summary>
public class StorageMangaEntryViewModel : BaseViewModel
{
    public Manga  Model    { get; }
    public string Title    => Model.Title;
    public long   Bytes    { get; }
    public string SizeText => StorageItemViewModel.FormatSize(Bytes);

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            SelectionChanged?.Invoke();
        }
    }

    // Deshabilitado cuando la colección entera está marcada para borrar —
    // el tomo ya va incluido, no tiene sentido elegirlo aparte.
    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public event Action? SelectionChanged;

    public StorageMangaEntryViewModel(Manga manga, long bytes)
    {
        Model = manga;
        Bytes = bytes;
    }
}
