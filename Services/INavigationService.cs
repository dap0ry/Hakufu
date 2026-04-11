using Hakufu.MVVM.ViewModel;

namespace Hakufu.Services;

public interface INavigationService
{
    BaseViewModel? CurrentViewModel { get; }
    void NavigateTo<TViewModel>() where TViewModel : BaseViewModel;
    void NavigateTo<TViewModel>(object parameter) where TViewModel : BaseViewModel;
}
