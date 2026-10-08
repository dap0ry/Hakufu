using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class MainWindowViewModel : BaseViewModel
{
    private readonly INavigationService _nav;
    private readonly DialogService _dialog;

    private BaseViewModel? _currentView;
    private BaseViewModel? _modalContent;
    private bool _isModalOpen;

    public MainWindowViewModel(INavigationService nav, DialogService dialog, UpdateBannerViewModel? updateBanner = null)
    {
        _nav = nav;
        _dialog = dialog;
        UpdateBanner = updateBanner;

        // Wire dialog callbacks into this VM
        _dialog.Register(
            show: vm => { ModalContent = vm; IsModalOpen = true; },
            close: () => { IsModalOpen = false; ModalContent = null; }
        );

        // Propagate navigation changes into CurrentView
        if (nav is Services.NavigationService ns)
            ns.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ns.CurrentViewModel))
                    CurrentView = ns.CurrentViewModel;
            };

        // Navigate to home on startup
        _nav.NavigateTo<HomeViewModel>();
    }

    /// <summary>Barra de «versión nueva» (null en los tests que no la montan).</summary>
    public UpdateBannerViewModel? UpdateBanner { get; }

    public BaseViewModel? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public BaseViewModel? ModalContent
    {
        get => _modalContent;
        private set => SetProperty(ref _modalContent, value);
    }

    public bool IsModalOpen
    {
        get => _isModalOpen;
        private set => SetProperty(ref _isModalOpen, value);
    }

    public RelayCommand CloseModalCommand => new(() => _dialog.CloseModal());
}
