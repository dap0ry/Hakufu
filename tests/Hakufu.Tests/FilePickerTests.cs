using Hakufu.Services;

namespace Hakufu.Tests;

public class FilePickerTests
{
    // Review Focus #5: guardar en iOS no pisa un archivo que ya está.
    [Fact]
    public void Unique_path_adds_a_number_when_the_name_is_taken()
    {
        using var tmp = new TempDataDir();
        Assert.Equal(Path.Combine(tmp.Root, "a.zip"), FilePickerService.UniquePath(tmp.Root, "a.zip"));

        File.WriteAllText(Path.Combine(tmp.Root, "a.zip"), "");
        Assert.Equal(Path.Combine(tmp.Root, "a (2).zip"), FilePickerService.UniquePath(tmp.Root, "a.zip"));

        File.WriteAllText(Path.Combine(tmp.Root, "a (2).zip"), "");
        Assert.Equal(Path.Combine(tmp.Root, "a (3).zip"), FilePickerService.UniquePath(tmp.Root, "a.zip"));
    }

    [Fact]
    public async Task On_mobile_saving_goes_to_the_library_folder_without_a_dialog()
    {
        using var tmp = new TempDataDir();
        using var mobile = new MobileMode(tmp.Root);
        File.WriteAllText(Path.Combine(tmp.Root, "Hakufu-2026-10-08.zip"), "");

        var path = await new FilePickerService().SaveFileAsync("Guardar", "Hakufu-2026-10-08.zip", FileFilter.Backup);

        Assert.Equal(Path.Combine(tmp.Root, "Hakufu-2026-10-08 (2).zip"), path);
    }

    [Fact]
    public async Task Picked_file_is_copied_to_a_local_folder()
    {
        using var tmp = new TempDataDir();

        var local = await FilePickerService.CopyToLocalAsync(
            "foto perfil.png", () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
            Path.Combine(tmp.Root, "copias"));

        Assert.Equal("foto perfil.png", Path.GetFileName(local));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(local));
    }

    [Fact]
    public void Display_path_on_mobile_names_the_files_app_folder()
    {
        using var tmp = new TempDataDir();
        var saved = Path.Combine(tmp.Root, "Hakufu-2026-10-08.zip");
        Assert.Equal(saved, FilePickerService.DisplayPath(saved)); // escritorio: la ruta tal cual

        using var mobile = new MobileMode(tmp.Root);
        Hakufu.I18n.Localizer.Instance.SetLanguage("es");
        Assert.Equal("Archivos → En mi iPhone/iPad → Hakufu → Hakufu-2026-10-08.zip", FilePickerService.DisplayPath(saved));
    }
}
