using Hakufu.MVVM.ViewModel;

namespace Hakufu.Services;

public class DialogService : IDialogService
{
    private Action<BaseViewModel>? _showAction;
    private Action? _closeAction;

    /// <summary>Called once by MainWindowViewModel to wire up the overlay callbacks.</summary>
    public void Register(Action<BaseViewModel> show, Action close)
    {
        _showAction = show;
        _closeAction = close;
    }

    public void ShowModal(BaseViewModel modalViewModel)
        => _showAction?.Invoke(modalViewModel);

    public void CloseModal()
        => _closeAction?.Invoke();
}
