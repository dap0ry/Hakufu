using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class AccountViewModel : BaseViewModel
{
    private readonly ISessionService    _session;
    private readonly HakufuApiClient    _api;
    private readonly INavigationService _nav;

    // ── Tab ─────────────────────────────────────────────────────────────────
    private bool _isLoginTab = true;
    public bool IsLoginTab
    {
        get => _isLoginTab;
        set { SetProperty(ref _isLoginTab, value); OnPropertyChanged(nameof(IsRegisterTab)); }
    }
    public bool IsRegisterTab => !IsLoginTab;

    // ── Login fields ─────────────────────────────────────────────────────────
    private string _loginUsername = "";
    public string LoginUsername
    {
        get => _loginUsername;
        set => SetProperty(ref _loginUsername, value);
    }
    public string LoginPassword { get; set; } = "";   // set from code-behind (PasswordBox)

    // ── Register fields ──────────────────────────────────────────────────────
    private string _regUsername = "";
    private string _regEmail    = "";
    public string RegUsername
    {
        get => _regUsername;
        set => SetProperty(ref _regUsername, value);
    }
    public string RegEmail
    {
        get => _regEmail;
        set => SetProperty(ref _regEmail, value);
    }
    public string RegPassword        { get; set; } = "";   // set from code-behind
    public string RegPasswordConfirm { get; set; } = "";   // set from code-behind

    // ── State ────────────────────────────────────────────────────────────────
    private string? _error;
    private bool    _isLoading;
    public string? ErrorMessage { get => _error;     private set => SetProperty(ref _error, value); }
    public bool    IsLoading    { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    public AccountViewModel(ISessionService session, HakufuApiClient api, INavigationService nav)
    {
        _session = session;
        _api     = api;
        _nav     = nav;
    }

    public RelayCommand SwitchToLoginCommand    => new(() => { IsLoginTab = true;  ErrorMessage = null; });
    public RelayCommand SwitchToRegisterCommand => new(() => { IsLoginTab = false; ErrorMessage = null; });
    public RelayCommand BackCommand             => new(() => _nav.NavigateTo<HomeViewModel>());

    public AsyncRelayCommand LoginCommand    => new(DoLoginAsync,    () => !IsLoading);
    public AsyncRelayCommand RegisterCommand => new(DoRegisterAsync, () => !IsLoading);

    private async Task DoLoginAsync()
    {
        ErrorMessage = null;
        IsLoading    = true;
        try
        {
            var result = await _api.LoginAsync(LoginUsername.Trim(), LoginPassword);
            _session.SetSession(result.Username, result.AccessToken);
            _nav.NavigateTo<HomeViewModel>();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    private async Task DoRegisterAsync()
    {
        ErrorMessage = null;
        IsLoading    = true;
        try
        {
            var result = await _api.RegisterAsync(
                RegUsername.Trim(), RegEmail.Trim(), RegPassword, RegPasswordConfirm);
            _session.SetSession(result.Username, result.AccessToken);
            _nav.NavigateTo<HomeViewModel>();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }
}
