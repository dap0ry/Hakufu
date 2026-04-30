namespace Hakufu.MVVM.ViewModel;

public class PublicHistoryItemViewModel : BaseViewModel
{
    public string Title    { get; }
    public string CoverUrl { get; }

    public PublicHistoryItemViewModel(string title, string coverUrl)
    {
        Title    = title;
        CoverUrl = coverUrl;
    }
}
