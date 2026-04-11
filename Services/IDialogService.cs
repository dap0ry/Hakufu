using Hakufu.MVVM.ViewModel;

namespace Hakufu.Services;

public interface IDialogService
{
    void ShowModal(BaseViewModel modalViewModel);
    void CloseModal();
}
