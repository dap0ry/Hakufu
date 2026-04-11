namespace Hakufu.Data;

public interface IDataRepository
{
    AppDataStore Current { get; }
    Task LoadAsync();
    Task SaveAsync();
}
