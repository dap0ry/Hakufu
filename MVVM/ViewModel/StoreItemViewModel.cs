using System.Collections.ObjectModel;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StoreItemViewModel : BaseViewModel
{
    private readonly CatalogItem _item;

    public string Title       => _item.Title;
    public string Author      => _item.Author;
    public string Description => _item.Description;
    public string CoverUrl    => _item.CoverUrl;
    public string TagsText    => string.Join("  ·  ", _item.Tags);
    public bool   HasTags     => _item.Tags.Count > 0;
    public bool   HasMeta     => _item.Pages > 0 || _item.SizeMb > 0;
    public string MetaText
    {
        get
        {
            var parts = new List<string>();
            if (_item.Pages > 0) parts.Add($"{_item.Pages} págs.");
            if (_item.SizeMb > 0) parts.Add($"{_item.SizeMb:0.#} MB");
            return string.Join("  ·  ", parts);
        }
    }

    public ObservableCollection<StoreVolumeViewModel> Volumes { get; } = new();

    public StoreItemViewModel(CatalogItem item, IStoreService store)
    {
        _item = item;
        foreach (var vol in item.Volumes)
            Volumes.Add(new StoreVolumeViewModel(vol, item.Title, store));
    }
}
