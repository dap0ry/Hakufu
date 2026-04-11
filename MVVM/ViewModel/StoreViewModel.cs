using System.Collections.ObjectModel;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StoreViewModel : BaseViewModel
{
    private readonly IStoreService      _store;
    private readonly INavigationService _nav;

    public ObservableCollection<StoreItemViewModel> Items { get; } = new();

    private bool   _isLoading = true;
    private bool   _hasError;
    private string _errorText = "";

    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(ShowItems)); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool HasError
    {
        get => _hasError;
        private set { SetProperty(ref _hasError, value); OnPropertyChanged(nameof(ShowItems)); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public string ErrorText { get => _errorText; private set => SetProperty(ref _errorText, value); }

    /// <summary>Lista cargada y con elementos.</summary>
    public bool ShowItems => !_isLoading && !_hasError && Items.Count > 0;

    /// <summary>Lista cargada, sin error y vacía.</summary>
    public bool IsEmpty   => !_isLoading && !_hasError && Items.Count == 0;

    public RelayCommand      GoBackCommand { get; }
    public AsyncRelayCommand RetryCommand  { get; }

    public StoreViewModel(IStoreService store, INavigationService nav)
    {
        _store = store;
        _nav   = nav;

        GoBackCommand = new RelayCommand(() => _nav.NavigateTo<HomeViewModel>());
        RetryCommand  = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        HasError  = false;
        Items.Clear();

        try
        {
            var catalog = await _store.FetchCatalogAsync();
            foreach (var item in catalog.Items)
                Items.Add(new StoreItemViewModel(item, _store));
        }
        catch (Exception ex)
        {
            HasError  = true;
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowItems));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
