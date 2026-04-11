using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public class StoreVolumeViewModel : BaseViewModel
{
    private readonly IStoreService _store;

    public string Label       { get; }
    public string DownloadUrl { get; }
    public bool   HasUrl      => !string.IsNullOrWhiteSpace(DownloadUrl);

    public RelayCommand OpenLinkCommand { get; }

    public StoreVolumeViewModel(CatalogVolume volume, IStoreService store)
    {
        Label       = volume.Label;
        DownloadUrl = volume.DownloadUrl;
        _store      = store;

        OpenLinkCommand = new RelayCommand(
            () => _store.OpenDownloadLink(DownloadUrl),
            () => HasUrl);
    }
}
