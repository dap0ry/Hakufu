using Microsoft.Win32;

namespace Hakufu.Services;

public class FilePickerService : IFilePickerService
{
    public string[] PickFiles(string title, string filter, bool multiSelect = true)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = multiSelect
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }
}
