namespace Hakufu.MVVM.ViewModel;

public class FriendItemViewModel : BaseViewModel
{
    public string Username  { get; }
    public string Initial   => Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
    public string AvatarUrl { get; }
    public bool   HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    public RelayCommand      ViewProfileCommand { get; }
    public AsyncRelayCommand RemoveCommand      { get; }

    public FriendItemViewModel(string username, string avatarUrl,
                                Action<string> viewProfile, Func<string, Task> remove)
    {
        Username           = username;
        AvatarUrl          = avatarUrl;
        ViewProfileCommand = new RelayCommand(() => viewProfile(username));
        RemoveCommand      = new AsyncRelayCommand(() => remove(username));
    }
}
