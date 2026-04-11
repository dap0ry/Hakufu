using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class CreateCollectionViewModel : BaseViewModel
{
    private readonly LibraryService  _library;
    private readonly IDialogService  _dialog;
    private readonly Func<Task>?     _onCreated;

    private string _name        = string.Empty;
    private string _description = string.Empty;

    public CreateCollectionViewModel(LibraryService library, IDialogService dialog, Func<Task>? onCreated = null)
    {
        _library   = library;
        _dialog    = dialog;
        _onCreated = onCreated;
    }

    public string Name
    {
        get => _name;
        set { SetProperty(ref _name, value); ConfirmCommand.RaiseCanExecuteChanged(); }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public RelayCommand ConfirmCommand => new(
        async () =>
        {
            await _library.CreateCollectionAsync(Name.Trim(), Description.Trim());
            _dialog.CloseModal();
            if (_onCreated is not null) await _onCreated();
        },
        () => !string.IsNullOrWhiteSpace(Name));

    public RelayCommand CancelCommand => new(() => _dialog.CloseModal());
}
