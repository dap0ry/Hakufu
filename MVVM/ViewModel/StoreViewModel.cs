using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public record BrowserBookmark(string Label, string Url);

public class StoreViewModel : BaseViewModel
{
    private readonly INavigationService _nav;
    private string _addressBarText = "https://tomosmanga.com/";

    public string AddressBarText
    {
        get => _addressBarText;
        set { _addressBarText = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<BrowserBookmark> Bookmarks { get; } = new List<BrowserBookmark>
    {
        new("Tomos Manga",  "https://tomosmanga.com/"),
        new("LexMangas",    "https://www.lexmangas.com/"),
        new("MangaYComics", "https://mangaycomics.com/"),
    };

    public RelayCommand           GoBackCommand        { get; }
    public RelayCommand           NavigateCommand      { get; }
    public RelayCommand<string>   NavigateToUrlCommand { get; }

    public event Action<string>? NavigationRequested;

    public StoreViewModel(IStoreService _, INavigationService nav)
    {
        _nav = nav;
        GoBackCommand = new RelayCommand(() => _nav.NavigateTo<HomeViewModel>());

        NavigateCommand = new RelayCommand(() =>
        {
            var url = NormalizeUrl(_addressBarText);
            AddressBarText = url;
            NavigationRequested?.Invoke(url);
        });

        NavigateToUrlCommand = new RelayCommand<string>(url =>
        {
            if (url is null) return;
            AddressBarText = url;
            NavigationRequested?.Invoke(url);
        });
    }

    public void OnNavigated(string url)
    {
        _addressBarText = url;
        OnPropertyChanged(nameof(AddressBarText));
    }

    private static string NormalizeUrl(string input)
    {
        input = input.Trim();
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "https" || uri.Scheme == "http"))
            return input;
        if (input.Contains('.') && !input.Contains(' '))
            return $"https://{input}";
        return $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";
    }
}
