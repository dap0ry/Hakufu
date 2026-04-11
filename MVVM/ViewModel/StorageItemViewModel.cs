namespace Hakufu.MVVM.ViewModel;

public class StorageItemViewModel : BaseViewModel
{
    public string Name     { get; }
    public string FullPath { get; }
    public long   Bytes    { get; }
    public string SizeText => FormatSize(Bytes);

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public StorageItemViewModel(string name, string fullPath, long bytes)
    {
        Name     = name;
        FullPath = fullPath;
        Bytes    = bytes;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
