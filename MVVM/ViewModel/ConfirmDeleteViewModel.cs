using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class ConfirmDeleteViewModel : BaseViewModel
{
    private readonly Func<Task>  _onConfirm;
    private readonly IDialogService _dialog;

    public string Title   { get; }
    public string Message { get; }

    public ConfirmDeleteViewModel(string title, string message,
                                  Func<Task> onConfirm, IDialogService dialog)
    {
        Title      = title;
        Message    = message;
        _onConfirm = onConfirm;
        _dialog    = dialog;
    }

    public RelayCommand ConfirmCommand => new(async () =>
    {
        _dialog.CloseModal();
        await _onConfirm();
    });

    public RelayCommand CancelCommand => new(() => _dialog.CloseModal());
}
