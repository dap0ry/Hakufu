using System.IO;
using System.Text.Json;

namespace Hakufu.Data;

public class JsonDataRepository : IDataRepository
{
    private static readonly string DataDir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu");

    private static readonly string DataFile = Path.Combine(DataDir, "data.json");
    private static readonly string TmpFile  = DataFile + ".tmp"; // solo para limpieza en LoadAsync

    private static readonly string OldDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hakufu");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // SemaphoreSlim(1,1) serializa escrituras; no usamos WaitAsync para evitar
    // deadlock cuando OnExit llama GetAwaiter().GetResult() en el hilo UI.
    private static readonly SemaphoreSlim _saveLock = new(1, 1);

    public AppDataStore Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        // One-time migration: %LOCALAPPDATA%\Hakufu → %APPDATA%\Hakufu
        if (!Directory.Exists(DataDir) && Directory.Exists(OldDataDir))
        {
            Directory.CreateDirectory(DataDir);
            var oldFile = Path.Combine(OldDataDir, "data.json");
            if (File.Exists(oldFile))
                File.Copy(oldFile, DataFile, overwrite: false);
        }

        // Limpieza: elimina cualquier .tmp huérfano de versiones anteriores
        try { if (File.Exists(TmpFile)) File.Delete(TmpFile); } catch { }

        if (!File.Exists(DataFile))
        {
            Current = new AppDataStore();
            return;
        }

        try
        {
            // FileShare.ReadWrite permite que SaveAsync escriba aunque este stream esté abierto
            await using var stream = new FileStream(
                DataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                bufferSize: 4096, useAsync: true);
            Current = await JsonSerializer.DeserializeAsync<AppDataStore>(stream, JsonOptions)
                      ?? new AppDataStore();

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
        // SemaphoreSlim garantiza que solo un SaveAsync corre a la vez.
        // ConfigureAwait(false) evita deadlock cuando OnExit llama
        // GetAwaiter().GetResult() desde el hilo UI.
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(DataDir);
            // FileShare.ReadWrite: permite lecturas concurrentes durante la escritura
            await using var stream = new FileStream(
                DataFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite,
                bufferSize: 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, Current, JsonOptions).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
