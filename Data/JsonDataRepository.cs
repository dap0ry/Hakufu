using System.Text.Json;

namespace Hakufu.Data;

public class JsonDataRepository : IDataRepository
{
    private static string DataDir  => AppPaths.DataDir;
    private static string DataFile => AppPaths.DataFile;
    private static string TmpFile  => DataFile + ".tmp"; // solo para limpieza en LoadAsync

    // Hasta la 0.9.x los datos vivían en %LOCALAPPDATA%\Hakufu (solo Windows).
    private static readonly string OldDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hakufu");

    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // SemaphoreSlim(1,1) serializa escrituras. ConfigureAwait(false) evita el
    // deadlock cuando el cierre de la app espera a SaveAsync desde el hilo UI.
    private static readonly SemaphoreSlim _saveLock = new(1, 1);

    public AppDataStore Current { get; private set; } = new();

    public void Replace(AppDataStore store) => Current = Normalize(store);

    public async Task LoadAsync()
    {
        // One-time migration: %LOCALAPPDATA%\Hakufu → %APPDATA%\Hakufu
        if (OperatingSystem.IsWindows() &&
            Environment.GetEnvironmentVariable("HAKUFU_DATA_DIR") is null or "" &&
            !Directory.Exists(DataDir) && Directory.Exists(OldDataDir))
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
            var stream = new FileStream(
                DataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                bufferSize: 4096, useAsync: true);
            // ConfigureAwait(false) en TODOS los await (también el DisposeAsync
            // del using): si alguien espera esto bloqueando el hilo de UI, una
            // continuación encolada en ese hilo sería un deadlock.
            await using (stream.ConfigureAwait(false))
            {
                Current = Normalize(await JsonSerializer.DeserializeAsync<AppDataStore>(stream, JsonOptions)
                                        .ConfigureAwait(false)
                                    ?? new AppDataStore());
            }
        }
        catch
        {
            Current = new AppDataStore();
        }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(DataDir);
            var stream = new FileStream(
                DataFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite,
                bufferSize: 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
                await JsonSerializer.SerializeAsync(stream, Current, JsonOptions).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // Siempre 3 huecos de favoritos, aunque el JSON venga de una versión vieja.
    private static AppDataStore Normalize(AppDataStore store)
    {
        while (store.Favorites.Count < 3)
            store.Favorites.Add(new() { SlotIndex = store.Favorites.Count });
        return store;
    }
}
