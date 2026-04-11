namespace Hakufu.MVVM.ViewModel;

public class CollectionStatViewModel
{
    public string CollectionName { get; }
    public int    PagesRead      { get; }
    public double BarPercent     { get; }   // 0..100

    public CollectionStatViewModel(string name, int pages, int maxPages)
    {
        CollectionName = name;
        PagesRead      = pages;
        BarPercent     = maxPages > 0 ? pages / (double)maxPages * 100.0 : 0;
    }
}
