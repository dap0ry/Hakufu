namespace Hakufu.Services;

public interface IFilePickerService
{
    /// <summary>Returns selected file paths, or empty array if cancelled.</summary>
    string[] PickFiles(string title, string filter, bool multiSelect = true);
}
