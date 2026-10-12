using System.Text.Json;

namespace Hakufu.Data;

public class JsonDataRepository : IDataRepository
{
    // La carpeta se fija al crear el repositorio y no cambia: un guardado que
    // termine tarde (en segundo plano) escribe donde se cargó, aunque
    // HAKUFU_DATA_DIR haya cambiado entre medias. Antes se leía en cada
    // guardado y un test que acababa podía escribir en la biblioteca real.
    private readonly string _dataDir = AppPaths.DataDir;
    private string DataDir  => _dataDir;
    private string DataFile => Path.Combine(_dataDir, "data.json");
    // Donde se escribe antes de sustituir data.json (ver SaveAsync). Si queda uno
    // al arrancar es de un guardado que no terminó: data.json es el bueno.
    private string TmpFile  => DataFile + ".tmp";

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
            // No se puede leer: se empieza de cero, pero el primer guardado lo pisaría
            // (y con él progreso, favoritos y perfil). Se aparta una copia antes.
            KeepUnreadable();
            Current = new AppDataStore();
        }
    }

    private void KeepUnreadable()
    {
        try
        {
            File.Copy(DataFile, Path.Combine(DataDir, $"data-ilegible-{DateTime.Now:yyyyMMdd-HHmmss}.json"),
                      overwrite: false);
        }
        catch { /* sin copia: al menos que arranque */ }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(DataDir);
            // Primero a data.json.tmp y después se sustituye data.json: si el guardado
            // falla a mitad (una lista que cambia mientras se escribe, la app que se
            // cierra…), data.json sigue entero. Escrito directamente quedaba cortado, al
            // arrancar no se podía leer y se empezaba de cero.
            var stream = new FileStream(
                TmpFile, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(stream, Current, JsonOptions).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            ReplaceDataFile();
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private void ReplaceDataFile()
    {
        try
        {
            File.Move(TmpFile, DataFile, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Windows no deja sustituir un archivo que otro tiene abierto (antivirus,
            // indexador): se copia encima, como se hacía antes.
            File.Copy(TmpFile, DataFile, overwrite: true);
            try { File.Delete(TmpFile); } catch { }
        }
    }

    // Un JSON viejo (o editado a mano) puede traer null donde ahora hay objetos.
    private static AppDataStore Normalize(AppDataStore store)
    {
        store.Profile    ??= new();
        store.ReadingLog ??= [];
        store.Reader     ??= new();
        store.LibraryRoot ??= "";
        return store;
    }
}
