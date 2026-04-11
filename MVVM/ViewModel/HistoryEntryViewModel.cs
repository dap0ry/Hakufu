using System.Windows.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class HistoryEntryViewModel : BaseViewModel
{
    public Manga    Manga    { get; }
    public DateTime ReadAt   { get; }
    public string   TimeAgo  => FormatTimeAgo(ReadAt);

    private BitmapSource? _cover;
    public BitmapSource?  Cover { get => _cover; private set => SetProperty(ref _cover, value); }

    public HistoryEntryViewModel(Manga manga, DateTime readAt)
    {
        Manga  = manga;
        ReadAt = readAt;
    }

    public async Task LoadCoverAsync(ICoverService coverService)
        => Cover = await coverService.GetCoverAsync(Manga);

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1)  return "Hace un momento";
        if (span.TotalHours   < 1)  return $"Hace {(int)span.TotalMinutes} min";
        if (span.TotalDays    < 1)  return $"Hace {(int)span.TotalHours} h";
        if (span.TotalDays    < 30) return $"Hace {(int)span.TotalDays} días";
        return dt.ToString("dd/MM/yyyy");
    }
}
