using System.IO;
using System.Text.Json;

namespace Hakufu.Data;

public class JsonDataRepository : IDataRepository
{
    private static readonly string DataDir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hakufu");

    private static readonly string DataFile = Path.Combine(DataDir, "data.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppDataStore Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        if (!File.Exists(DataFile))
        {
            Current = new AppDataStore();
            return;
        }

        try
        {
            await using var stream = File.OpenRead(DataFile);
            Current = await JsonSerializer.DeserializeAsync<AppDataStore>(stream, JsonOptions)
                      ?? new AppDataStore();

            // Ensure favorites always has 3 slots
            while (Current.Favorites.Count < 3)
                Current.Favorites.Add(new() { SlotIndex = Current.Favorites.Count });
        }
        catch
        {
            Current = new AppDataStore();
        }
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(DataDir);
        await using var stream = File.Create(DataFile);
        await JsonSerializer.SerializeAsync(stream, Current, JsonOptions);
    }
}
