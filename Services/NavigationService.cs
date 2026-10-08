using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Services;

public class NavigationService : INavigationService, INotifyPropertyChanged
{
    private readonly Func<Type, object?, BaseViewModel> _factory;
    private BaseViewModel? _currentViewModel;
    private Type? _lastType;
    private object? _lastParam;

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
        => Go(typeof(TViewModel), null);

    public void NavigateTo<TViewModel>(object parameter) where TViewModel : BaseViewModel
        => Go(typeof(TViewModel), parameter);

    public void Reload()
    {
        if (_lastType is not null) Go(_lastType, _lastParam);
    }

    private void Go(Type type, object? param)
    {
        _lastType = type;
        _lastParam = param;
        CurrentViewModel = _factory(type, param);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
