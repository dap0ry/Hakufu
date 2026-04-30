namespace Hakufu.MVVM.ViewModel;

public class FriendRequestItemViewModel : BaseViewModel
{
    public string From      { get; }
    public string Initial   => From.Length > 0 ? From[0].ToString().ToUpper() : "?";
    public string AvatarUrl { get; }
    public bool   HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    public AsyncRelayCommand AcceptCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }

    public FriendRequestItemViewModel(string from, string avatarUrl,
                                       Func<string, Task> accept, Func<string, Task> reject)
    {
        From          = from;
        AvatarUrl     = avatarUrl;
        AcceptCommand = new AsyncRelayCommand(() => accept(from));
        RejectCommand = new AsyncRelayCommand(() => reject(from));
    }
}
