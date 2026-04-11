using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public class StoreService : IStoreService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MangaCatalog LoadCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "catalog", "catalog.json");
        if (!File.Exists(path)) return new MangaCatalog();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MangaCatalog>(json, JsonOpts) ?? new MangaCatalog();
        }
        catch
        {
            return new MangaCatalog();
        }
    }

    public void OpenDownloadLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
