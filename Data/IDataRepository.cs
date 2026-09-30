namespace Hakufu.Data;

public interface IDataRepository
{
    AppDataStore Current { get; }
    Task LoadAsync();
    Task SaveAsync();

    /// <summary>Sustituye todos los datos en memoria (p. ej. al importar una copia). No guarda.</summary>
    void Replace(AppDataStore store);
}
