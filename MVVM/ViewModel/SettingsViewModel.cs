using Hakufu.Data;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class SettingsViewModel : BaseViewModel
{
    private readonly IThemeService      _theme;
    private readonly IDataRepository    _repo;
    private readonly INavigationService _nav;

    public SettingsViewModel(IThemeService theme, IDataRepository repo, INavigationService nav)
    {
        _theme = theme;
        _repo  = repo;
        _nav   = nav;
        _isDarkTheme = _theme.CurrentTheme == AppTheme.Dark;
    }

    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (!SetProperty(ref _isDarkTheme, value)) return;
            var t = value ? AppTheme.Dark : AppTheme.Light;
            _theme.SetTheme(t);
            _repo.Current.ActiveTheme = value ? "Dark" : "Light";
            _ = _repo.SaveAsync();
        }
    }

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());
}
