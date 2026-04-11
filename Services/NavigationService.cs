using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Services;

public class NavigationService : INavigationService, INotifyPropertyChanged
{
    private readonly Func<Type, object?, BaseViewModel> _factory;
    private BaseViewModel? _currentViewModel;

    public NavigationService(Func<Type, object?, BaseViewModel> factory)
    {
        _factory = factory;
    }

    public BaseViewModel? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            _currentViewModel = value;
            OnPropertyChanged();
        }
    }

    public void NavigateTo<TViewModel>() where TViewModel : BaseViewModel
        => CurrentViewModel = _factory(typeof(TViewModel), null);

    public void NavigateTo<TViewModel>(object parameter) where TViewModel : BaseViewModel
        => CurrentViewModel = _factory(typeof(TViewModel), parameter);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
