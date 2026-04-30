using System.Collections.ObjectModel;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class FriendsViewModel : BaseViewModel
{
    private readonly ISessionService    _session;
    private readonly HakufuApiClient    _api;
    private readonly INavigationService _nav;

    public ObservableCollection<FriendItemViewModel>        Friends        { get; } = [];
    public ObservableCollection<FriendRequestItemViewModel> PendingRequests { get; } = [];

    public bool IsLoggedIn        => _session.IsLoggedIn;
    public bool HasPendingRequests => PendingRequests.Count > 0;
    public bool HasFriends         => Friends.Count > 0;

    private string  _searchQuery  = "";
    private string? _statusMessage;
    private bool    _isLoading;

    public string  SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public FriendsViewModel(ISessionService session, HakufuApiClient api, INavigationService nav)
    {
        _session = session;
        _api     = api;
        _nav     = nav;
        if (_session.IsLoggedIn) _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = null;
        try
        {
            var friends  = await _api.GetFriendsAsync();
            var requests = await _api.GetPendingRequestsAsync();

            Friends.Clear();
            foreach (var u in friends)
                Friends.Add(new FriendItemViewModel(u.Username, u.AvatarUrl,
                    viewProfile: un => _nav.NavigateTo<PublicProfileViewModel>(un),
                    remove: async un =>
                    {
                        await _api.RemoveFriendAsync(un);
                        await LoadAsync();
                    }));

            PendingRequests.Clear();
            foreach (var r in requests)
                PendingRequests.Add(new FriendRequestItemViewModel(r.From, r.AvatarUrl,
                    accept: async from =>
                    {
                        await _api.AcceptFriendRequestAsync(from);
                        await LoadAsync();
                    },
                    reject: async from =>
                    {
                        await _api.RejectFriendRequestAsync(from);
                        await LoadAsync();
                    }));

            OnPropertyChanged(nameof(HasFriends));
            OnPropertyChanged(nameof(HasPendingRequests));
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private async Task DoSendRequestAsync()
    {
        var target = SearchQuery.Trim();
        if (string.IsNullOrEmpty(target)) return;
        StatusMessage = null;
        IsLoading     = true;
        try
        {
            await _api.SendFriendRequestAsync(target);
            StatusMessage = $"Solicitud enviada a {target}";
            SearchQuery   = "";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    public AsyncRelayCommand SendRequestCommand =>
        new(DoSendRequestAsync, () => !IsLoading && !string.IsNullOrWhiteSpace(SearchQuery));

    public RelayCommand BackCommand     => new(() => _nav.NavigateTo<HomeViewModel>());
    public RelayCommand NavLoginCommand => new(() => _nav.NavigateTo<AccountViewModel>());
}
