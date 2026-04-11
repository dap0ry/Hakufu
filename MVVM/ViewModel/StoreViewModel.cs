using System.Collections.ObjectModel;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StoreViewModel : BaseViewModel
{
    private readonly INavigationService _nav;

    public ObservableCollection<StoreItemViewModel> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;

    public RelayCommand GoBackCommand { get; }

    public StoreViewModel(IStoreService store, INavigationService nav)
    {
        _nav = nav;
        GoBackCommand = new RelayCommand(() => _nav.NavigateTo<HomeViewModel>());

        var catalog = store.LoadCatalog();
        foreach (var item in catalog.Items)
            Items.Add(new StoreItemViewModel(item, store));
    }
}
