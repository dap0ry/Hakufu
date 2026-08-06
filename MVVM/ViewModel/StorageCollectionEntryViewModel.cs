using System.Collections.ObjectModel;
using Hakufu.MVVM.Model;

namespace Hakufu.MVVM.ViewModel;

/// <summary>Una colección en el árbol de "Gestionar espacio", con sus tomos debajo.</summary>
public class StorageCollectionEntryViewModel : BaseViewModel
{
    public Collection Model { get; }
    public string     Name  => Model.Name;

    public ObservableCollection<StorageMangaEntryViewModel> Mangas { get; } = [];

    public long   Bytes    => Mangas.Sum(m => m.Bytes);
    public string SizeText => StorageItemViewModel.FormatSize(Bytes);
    public string CountText => $"{Mangas.Count} tomo{(Mangas.Count != 1 ? "s" : "")}";

    private bool _isExpanded = true;
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

    // true = se borra la colección entera (todos sus tomos quedan marcados y bloqueados).
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            foreach (var manga in Mangas)
            {
                manga.IsEnabled = !value;
                manga.IsSelected = value;
            }
            SelectionChanged?.Invoke();
        }
    }

    public event Action? SelectionChanged;

    public StorageCollectionEntryViewModel(Collection collection)
    {
        Model = collection;
    }

    public void AddManga(Manga manga, long bytes)
    {
        var entry = new StorageMangaEntryViewModel(manga, bytes);
        entry.SelectionChanged += () => SelectionChanged?.Invoke();
        Mangas.Add(entry);
        OnPropertyChanged(nameof(Bytes));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(CountText));
    }

    /// <summary>Tomos marcados individualmente (sin contar los que van dentro de una colección ya marcada entera).</summary>
    public IEnumerable<StorageMangaEntryViewModel> IndividuallySelectedMangas
        => IsSelected ? [] : Mangas.Where(m => m.IsSelected);
}
