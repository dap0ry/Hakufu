using Hakufu.MVVM.Model;

namespace Hakufu.Data;

public class AppDataStore
{
    public List<Manga>               Mangas          { get; set; } = [];
    public List<Collection>          Collections     { get; set; } = [];
    public List<ReadingProgress>     Progress        { get; set; } = [];
    public List<FavoriteSlot>        Favorites       { get; set; } =
    [
        new() { SlotIndex = 0 },
        new() { SlotIndex = 1 },
        new() { SlotIndex = 2 }
    ];
    public List<ReadingHistoryEntry> History         { get; set; } = [];
    public string                    ActiveTheme     { get; set; } = "Light";
    public long                      TotalUsageSeconds { get; set; } = 0;
}
