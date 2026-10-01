using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class HelpViewModel : BaseViewModel
{
    private readonly INavigationService _nav;

    public HelpViewModel(INavigationService nav) => _nav = nav;

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());
}
